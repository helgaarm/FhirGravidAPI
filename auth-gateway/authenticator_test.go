package main

import (
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/rsa"
	"crypto/sha256"
	"encoding/base64"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strings"
	"testing"
	"time"

	dpop "github.com/AxisCommunications/go-dpop"
	"github.com/golang-jwt/jwt/v5"
)

const (
	testIssuer   = "https://helseid-sts.test.nhn.no"
	testAudience = "nhn:population-data-facade"
	testScope    = "nhn:population-data-facade/read"
)

func TestGatewayValidatesDPoPAndForwardsOnlyItsOwnSecret(t *testing.T) {
	gateway, upstreamSecret, accessToken, proof := authenticatedTestGateway(t, testScope, "/fhir/Patient/123")
	request := httptest.NewRequest(http.MethodGet, "https://facade.example/fhir/Patient/123?ignored=true", nil)
	request.Header.Set("Authorization", "DPoP "+accessToken)
	request.Header.Set("DPoP", proof)
	request.Header.Set(gatewaySecretHeader, "attacker-controlled")
	response := httptest.NewRecorder()

	gateway.ServeHTTP(response, request)

	if response.Code != http.StatusNoContent {
		t.Fatalf("expected 204, got %d: %s", response.Code, response.Body.String())
	}
	if got := <-upstreamSecret; got != strings.Repeat("g", 32) {
		t.Fatalf("upstream received the wrong gateway secret: %q", got)
	}
}

func TestGatewayRejectsReplayedProof(t *testing.T) {
	gateway, _, accessToken, proof := authenticatedTestGateway(t, testScope, "/fhir/Patient/123")

	for attempt, expected := range []int{http.StatusNoContent, http.StatusUnauthorized} {
		request := httptest.NewRequest(http.MethodGet, "https://facade.example/fhir/Patient/123", nil)
		request.Header.Set("Authorization", "DPoP "+accessToken)
		request.Header.Set("DPoP", proof)
		response := httptest.NewRecorder()
		gateway.ServeHTTP(response, request)
		if response.Code != expected {
			t.Fatalf("attempt %d: expected %d, got %d", attempt+1, expected, response.Code)
		}
	}
}

func TestGatewayRejectsBearerDowngrade(t *testing.T) {
	gateway, _, accessToken, _ := authenticatedTestGateway(t, testScope, "/fhir/Patient/123")
	request := httptest.NewRequest(http.MethodGet, "https://facade.example/fhir/Patient/123", nil)
	request.Header.Set("Authorization", "Bearer "+accessToken)
	response := httptest.NewRecorder()

	gateway.ServeHTTP(response, request)

	if response.Code != http.StatusUnauthorized {
		t.Fatalf("expected 401, got %d", response.Code)
	}
	if response.Header().Get("WWW-Authenticate") != "DPoP" {
		t.Fatalf("expected DPoP challenge, got %q", response.Header().Get("WWW-Authenticate"))
	}
	assertOperationOutcome(t, response, "security")
}

func TestGatewayRejectsProofForAnotherTarget(t *testing.T) {
	gateway, _, accessToken, proof := authenticatedTestGateway(t, testScope, "/fhir/Patient/other")
	request := httptest.NewRequest(http.MethodGet, "https://facade.example/fhir/Patient/123", nil)
	request.Header.Set("Authorization", "DPoP "+accessToken)
	request.Header.Set("DPoP", proof)
	response := httptest.NewRecorder()

	gateway.ServeHTTP(response, request)

	if response.Code != http.StatusUnauthorized {
		t.Fatalf("expected 401, got %d", response.Code)
	}
}

func TestGatewayRejectsARequestForAnotherHost(t *testing.T) {
	gateway, _, accessToken, proof := authenticatedTestGateway(t, testScope, "/fhir/Patient/123")
	request := httptest.NewRequest(http.MethodGet, "https://another.example/fhir/Patient/123", nil)
	request.Header.Set("Authorization", "DPoP "+accessToken)
	request.Header.Set("DPoP", proof)
	response := httptest.NewRecorder()

	gateway.ServeHTTP(response, request)

	if response.Code != http.StatusUnauthorized {
		t.Fatalf("expected 401, got %d", response.Code)
	}
}

func TestGatewayReturnsForbiddenForMissingScope(t *testing.T) {
	gateway, _, accessToken, proof := authenticatedTestGateway(t, "unrelated/scope", "/fhir/Patient/123")
	request := httptest.NewRequest(http.MethodGet, "https://facade.example/fhir/Patient/123", nil)
	request.Header.Set("Authorization", "DPoP "+accessToken)
	request.Header.Set("DPoP", proof)
	response := httptest.NewRecorder()

	gateway.ServeHTTP(response, request)

	if response.Code != http.StatusForbidden {
		t.Fatalf("expected 403, got %d", response.Code)
	}
	if response.Header().Get("WWW-Authenticate") != "" {
		t.Fatalf("forbidden response must not include a challenge: %q", response.Header().Get("WWW-Authenticate"))
	}
	assertOperationOutcome(t, response, "forbidden")
}

func authenticatedTestGateway(
	t *testing.T,
	scope string,
	proofPath string,
) (*gatewayHandler, <-chan string, string, string) {
	t.Helper()
	secretSeen := make(chan string, 1)
	upstream := httptest.NewServer(http.HandlerFunc(func(response http.ResponseWriter, request *http.Request) {
		secretSeen <- request.Header.Get(gatewaySecretHeader)
		response.WriteHeader(http.StatusNoContent)
	}))
	t.Cleanup(upstream.Close)
	upstreamURL, _ := url.Parse(upstream.URL)

	accessSigningKey, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		t.Fatal(err)
	}
	proofKey, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		t.Fatal(err)
	}

	target, _ := url.Parse("https://facade.example" + proofPath)
	thumbprintProof, err := dpop.Create(jwt.SigningMethodES256, proofClaims(target, "thumbprint", ""), proofKey)
	if err != nil {
		t.Fatal(err)
	}
	parsedProof, err := dpop.Parse(thumbprintProof, dpop.GET, target, dpop.ParseOptions{
		AllowedProofAge: durationPointer(proofLifetime),
		TimeWindow:      durationPointer(proofClockSkew),
	})
	if err != nil {
		t.Fatal(err)
	}

	now := time.Now()
	claims := &accessTokenClaims{
		BoundAccessTokenClaims: dpop.BoundAccessTokenClaims{
			RegisteredClaims: &jwt.RegisteredClaims{
				Issuer:    testIssuer,
				Audience:  jwt.ClaimStrings{testAudience},
				ExpiresAt: jwt.NewNumericDate(now.Add(5 * time.Minute)),
				NotBefore: jwt.NewNumericDate(now.Add(-time.Second)),
				IssuedAt:  jwt.NewNumericDate(now),
			},
			Confirmation: dpop.Confirmation{JWKThumbprint: parsedProof.PublicKey()},
		},
		Scope: scopeClaim{scope},
	}
	accessJWT := jwt.NewWithClaims(jwt.SigningMethodRS256, claims)
	accessJWT.Header["typ"] = "at+jwt"
	accessToken, err := accessJWT.SignedString(accessSigningKey)
	if err != nil {
		t.Fatal(err)
	}
	hash := sha256.Sum256([]byte(accessToken))
	proof, err := dpop.Create(
		jwt.SigningMethodES256,
		proofClaims(target, "request-proof", base64.RawURLEncoding.EncodeToString(hash[:])),
		proofKey,
	)
	if err != nil {
		t.Fatal(err)
	}

	validator := &helseIDTokenValidator{
		issuer:   testIssuer,
		audience: testAudience,
		keyFunc: func(token *jwt.Token) (any, error) {
			return &accessSigningKey.PublicKey, nil
		},
	}
	replays := newMemoryReplayStore()
	auth := newAuthenticator(validator, replays, "https", "facade.example", testScope)
	cfg := config{
		mode:           modeAuthenticate,
		upstream:       upstreamURL,
		externalScheme: "https",
		sharedSecret:   strings.Repeat("g", 32),
	}
	return newGatewayHandler(cfg, auth), secretSeen, accessToken, proof
}

func proofClaims(target *url.URL, id, accessTokenHash string) *dpop.ProofTokenClaims {
	return &dpop.ProofTokenClaims{
		RegisteredClaims: &jwt.RegisteredClaims{
			ID:       id,
			IssuedAt: jwt.NewNumericDate(time.Now()),
		},
		Method:          dpop.GET,
		URL:             target.String(),
		AccessTokenHash: accessTokenHash,
	}
}

func TestPassthroughStripsSpoofedGatewaySecret(t *testing.T) {
	secretSeen := make(chan string, 1)
	upstream := httptest.NewServer(http.HandlerFunc(func(response http.ResponseWriter, request *http.Request) {
		secretSeen <- request.Header.Get(gatewaySecretHeader)
		response.WriteHeader(http.StatusNoContent)
	}))
	defer upstream.Close()
	upstreamURL, _ := url.Parse(upstream.URL)
	gateway := newGatewayHandler(config{
		mode:           modePassthrough,
		upstream:       upstreamURL,
		externalScheme: "https",
	}, nil)
	request := httptest.NewRequest(http.MethodGet, "https://facade.example/test", nil)
	request.Header.Set(gatewaySecretHeader, "spoofed")
	response := httptest.NewRecorder()

	gateway.ServeHTTP(response, request)

	if response.Code != http.StatusNoContent {
		t.Fatalf("expected 204, got %d", response.Code)
	}
	if got := <-secretSeen; got != "" {
		t.Fatalf("spoofed secret reached upstream: %q", got)
	}
}
