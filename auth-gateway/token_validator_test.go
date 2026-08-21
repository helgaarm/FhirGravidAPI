package main

import (
	"context"
	"crypto/rand"
	"crypto/rsa"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"github.com/golang-jwt/jwt/v5"
)

func TestScopeClaimAcceptsSupportedEncodings(t *testing.T) {
	tests := []struct {
		name     string
		raw      string
		expected []string
	}{
		{"space separated", `"scope.one scope.two"`, []string{"scope.one", "scope.two"}},
		{"array", `["scope.one","scope.two"]`, []string{"scope.one", "scope.two"}},
	}
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			var claim scopeClaim
			if err := json.Unmarshal([]byte(test.raw), &claim); err != nil {
				t.Fatalf("unexpected error: %v", err)
			}
			if len(claim) != len(test.expected) {
				t.Fatalf("expected %v, got %v", test.expected, claim)
			}
			for index := range claim {
				if claim[index] != test.expected[index] {
					t.Fatalf("expected %v, got %v", test.expected, claim)
				}
			}
		})
	}
}

func TestScopeClaimRejectsMalformedEncodings(t *testing.T) {
	for _, raw := range []string{`null`, `42`, `{}`, `["scope.one",42]`, `[""]`} {
		t.Run(raw, func(t *testing.T) {
			var claim scopeClaim
			if err := json.Unmarshal([]byte(raw), &claim); err == nil {
				t.Fatalf("expected %s to be rejected", raw)
			}
		})
	}
}

func TestHelseIDTokenValidatorAcceptsSupportedAlgorithms(t *testing.T) {
	key := mustRSAKey(t)
	validator := testTokenValidator(key)
	for _, method := range []jwt.SigningMethod{jwt.SigningMethodRS256, jwt.SigningMethodPS256} {
		t.Run(method.Alg(), func(t *testing.T) {
			raw := signAccessToken(t, method, key, validAccessTokenClaims(), "at+jwt")

			if _, claims, err := validator.validate(context.Background(), raw); err != nil {
				t.Fatalf("expected valid token, got %v", err)
			} else if !claims.Scope.contains(testScope) {
				t.Fatalf("expected scope %q in %v", testScope, claims.Scope)
			}
		})
	}
}

func TestHelseIDTokenValidatorRejectsInvalidTrustClaims(t *testing.T) {
	key := mustRSAKey(t)
	otherKey := mustRSAKey(t)
	now := time.Now()
	tests := []struct {
		name   string
		method jwt.SigningMethod
		key    any
		typeID string
		mutate func(jwt.MapClaims)
	}{
		{"invalid signature", jwt.SigningMethodRS256, otherKey, "at+jwt", func(jwt.MapClaims) {}},
		{"wrong issuer", jwt.SigningMethodRS256, key, "at+jwt", func(claims jwt.MapClaims) { claims["iss"] = "https://issuer.example" }},
		{"wrong audience", jwt.SigningMethodRS256, key, "at+jwt", func(claims jwt.MapClaims) { claims["aud"] = "other-audience" }},
		{"multiple audiences", jwt.SigningMethodRS256, key, "at+jwt", func(claims jwt.MapClaims) { claims["aud"] = []string{testAudience, "other-audience"} }},
		{"expired", jwt.SigningMethodRS256, key, "at+jwt", func(claims jwt.MapClaims) { claims["exp"] = now.Add(-time.Minute).Unix() }},
		{"missing expiry", jwt.SigningMethodRS256, key, "at+jwt", func(claims jwt.MapClaims) { delete(claims, "exp") }},
		{"missing not-before", jwt.SigningMethodRS256, key, "at+jwt", func(claims jwt.MapClaims) { delete(claims, "nbf") }},
		{"future not-before", jwt.SigningMethodRS256, key, "at+jwt", func(claims jwt.MapClaims) { claims["nbf"] = now.Add(time.Minute).Unix() }},
		{"wrong token type", jwt.SigningMethodRS256, key, "JWT", func(jwt.MapClaims) {}},
		{"unsupported algorithm", jwt.SigningMethodHS256, []byte(strings.Repeat("h", 32)), "at+jwt", func(jwt.MapClaims) {}},
	}
	validator := testTokenValidator(key)
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			claims := validAccessTokenClaims()
			test.mutate(claims)
			raw := signAccessToken(t, test.method, test.key, claims, test.typeID)

			if _, _, err := validator.validate(context.Background(), raw); err == nil {
				t.Fatal("expected token validation to fail")
			}
		})
	}
}

func TestHelseIDTokenValidatorHonorsCancellation(t *testing.T) {
	key := mustRSAKey(t)
	raw := signAccessToken(t, jwt.SigningMethodRS256, key, validAccessTokenClaims(), "at+jwt")
	ctx, cancel := context.WithCancel(context.Background())
	cancel()

	if _, _, err := testTokenValidator(key).validate(ctx, raw); err == nil {
		t.Fatal("expected canceled validation to fail")
	}
}

func TestFetchDiscoveryValidatesTheAuthorityBoundary(t *testing.T) {
	tests := []struct {
		name      string
		status    int
		body      func(string) string
		wantError bool
	}{
		{"valid", http.StatusOK, func(authority string) string { return discoveryJSON(authority, authority+"/jwks") }, false},
		{"unexpected status", http.StatusBadGateway, func(string) string { return `{}` }, true},
		{"issuer mismatch", http.StatusOK, func(authority string) string { return discoveryJSON("https://issuer.example", authority+"/jwks") }, true},
		{"non TLS JWKS", http.StatusOK, func(authority string) string {
			return discoveryJSON(authority, strings.Replace(authority, "https://", "http://", 1)+"/jwks")
		}, true},
		{"cross host JWKS", http.StatusOK, func(authority string) string { return discoveryJSON(authority, "https://keys.example/jwks") }, true},
		{"credential bearing JWKS", http.StatusOK, func(authority string) string {
			return discoveryJSON(authority, strings.Replace(authority, "https://", "https://user:password@", 1)+"/jwks")
		}, true},
		{"malformed JSON", http.StatusOK, func(string) string { return `{` }, true},
		{"trailing JSON", http.StatusOK, func(authority string) string { return discoveryJSON(authority, authority+"/jwks") + `{}` }, true},
		{"oversized", http.StatusOK, func(string) string { return strings.Repeat(" ", discoveryResponseLimit+1) }, true},
	}
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			var server *httptest.Server
			server = httptest.NewTLSServer(http.HandlerFunc(func(response http.ResponseWriter, request *http.Request) {
				if request.URL.Path != "/.well-known/openid-configuration" {
					t.Fatalf("unexpected discovery path %q", request.URL.Path)
				}
				response.WriteHeader(test.status)
				_, _ = response.Write([]byte(test.body(server.URL)))
			}))
			defer server.Close()

			_, err := fetchDiscovery(context.Background(), server.Client(), server.URL)

			if test.wantError && err == nil {
				t.Fatal("expected discovery validation to fail")
			}
			if !test.wantError && err != nil {
				t.Fatalf("unexpected error: %v", err)
			}
		})
	}
}

func validAccessTokenClaims() jwt.MapClaims {
	now := time.Now()
	return jwt.MapClaims{
		"iss":   testIssuer,
		"aud":   testAudience,
		"exp":   now.Add(5 * time.Minute).Unix(),
		"nbf":   now.Add(-time.Second).Unix(),
		"iat":   now.Unix(),
		"scope": []string{testScope},
		"cnf":   map[string]string{"jkt": "test-thumbprint"},
	}
}

func mustRSAKey(t *testing.T) *rsa.PrivateKey {
	t.Helper()
	key, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		t.Fatal(err)
	}
	return key
}

func testTokenValidator(key *rsa.PrivateKey) *helseIDTokenValidator {
	return &helseIDTokenValidator{
		issuer:   testIssuer,
		audience: testAudience,
		keyFunc: func(*jwt.Token) (any, error) {
			return &key.PublicKey, nil
		},
	}
}

func signAccessToken(t *testing.T, method jwt.SigningMethod, key any, claims jwt.MapClaims, typeID string) string {
	t.Helper()
	token := jwt.NewWithClaims(method, claims)
	if typeID != "" {
		token.Header["typ"] = typeID
	}
	raw, err := token.SignedString(key)
	if err != nil {
		t.Fatal(err)
	}
	return raw
}

func discoveryJSON(issuer, jwksURL string) string {
	data, _ := json.Marshal(discoveryDocument{Issuer: issuer, JWKSURL: jwksURL})
	return string(data)
}
