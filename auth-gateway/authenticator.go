package main

import (
	"crypto/sha256"
	"encoding/base64"
	"encoding/hex"
	"encoding/json"
	"errors"
	"net/http"
	"net/url"
	"strings"
	"time"

	dpop "github.com/AxisCommunications/go-dpop"
)

const (
	maximumAccessTokenLength = 64 << 10
	maximumDPoPProofLength   = 16 << 10
	proofLifetime            = 10 * time.Second
	proofClockSkew           = 3 * time.Second
)

var allowedProofAlgorithms = map[string]string{
	"RS256": "RSA", "RS384": "RSA", "RS512": "RSA",
	"PS256": "RSA", "PS384": "RSA", "PS512": "RSA",
	"ES256": "EC", "ES384": "EC", "ES512": "EC",
}

type authenticationError struct {
	status int
	code   string
}

func (err *authenticationError) Error() string { return err.code }

type authenticator struct {
	tokens         accessTokenValidator
	replays        replayStore
	externalScheme string
	externalHost   string
	requiredScope  string
}

func newAuthenticator(
	tokens accessTokenValidator,
	replays replayStore,
	externalScheme string,
	externalHost string,
	requiredScope string,
) *authenticator {
	return &authenticator{
		tokens:         tokens,
		replays:        replays,
		externalScheme: externalScheme,
		externalHost:   externalHost,
		requiredScope:  requiredScope,
	}
}

func (auth *authenticator) authenticate(request *http.Request) error {
	if !strings.EqualFold(request.Host, auth.externalHost) {
		return unauthorized("invalid_request_host")
	}
	accessToken, err := parseAuthorization(request.Header.Values("Authorization"))
	if err != nil {
		return unauthorized("invalid_authorization")
	}
	proofText, err := exactlyOneHeader(request.Header.Values("DPoP"), maximumDPoPProofLength)
	if err != nil {
		return unauthorized("invalid_dpop_header")
	}
	if err := validateProofHeader(proofText); err != nil {
		return unauthorized("invalid_dpop_proof")
	}

	token, claims, err := auth.tokens.validate(request.Context(), accessToken)
	if err != nil {
		return unauthorized("invalid_access_token")
	}

	target := &url.URL{
		Scheme:  auth.externalScheme,
		Host:    auth.externalHost,
		Path:    request.URL.Path,
		RawPath: request.URL.RawPath,
	}
	proof, err := dpop.Parse(
		proofText,
		dpop.HTTPVerb(request.Method),
		target,
		dpop.ParseOptions{AllowedProofAge: durationPointer(proofLifetime), TimeWindow: durationPointer(proofClockSkew)},
	)
	if err != nil {
		return unauthorized("invalid_dpop_proof")
	}

	hash := sha256.Sum256([]byte(accessToken))
	accessTokenHash := base64.RawURLEncoding.EncodeToString(hash[:])
	if err := proof.Validate([]byte(accessTokenHash), token); err != nil {
		return unauthorized("invalid_dpop_binding")
	}
	proofClaims, ok := proof.Claims.(*dpop.ProofTokenClaims)
	if !ok || proofClaims.ID == "" || proofClaims.IssuedAt == nil {
		return unauthorized("invalid_dpop_claims")
	}

	replayDigest := sha256.Sum256([]byte(proof.PublicKey() + "\x00" + proofClaims.ID))
	expiresAt := proofClaims.IssuedAt.Time.Add(proofLifetime + proofClockSkew)
	accepted, err := auth.replays.use(request.Context(), hex.EncodeToString(replayDigest[:]), expiresAt)
	if err != nil {
		return unauthorized("replay_store_unavailable")
	}
	if !accepted {
		return unauthorized("replayed_dpop_proof")
	}

	if !claims.Scope.contains(auth.requiredScope) {
		return &authenticationError{status: http.StatusForbidden, code: "insufficient_scope"}
	}
	return nil
}

func parseAuthorization(values []string) (string, error) {
	value, err := exactlyOneHeader(values, maximumAccessTokenLength+16)
	if err != nil || strings.Contains(value, ",") {
		return "", errors.New("invalid Authorization header")
	}
	parts := strings.Fields(value)
	if len(parts) != 2 || !strings.EqualFold(parts[0], "DPoP") || len(parts[1]) > maximumAccessTokenLength {
		return "", errors.New("Authorization must contain one DPoP token")
	}
	return parts[1], nil
}

func exactlyOneHeader(values []string, maximumLength int) (string, error) {
	if len(values) != 1 || values[0] == "" || len(values[0]) > maximumLength || strings.Contains(values[0], ",") {
		return "", errors.New("expected exactly one header value")
	}
	return values[0], nil
}

type proofHeader struct {
	Type      string                     `json:"typ"`
	Algorithm string                     `json:"alg"`
	JWK       map[string]json.RawMessage `json:"jwk"`
}

func validateProofHeader(raw string) error {
	parts := strings.Split(raw, ".")
	if len(parts) != 3 {
		return errors.New("DPoP proof is not a compact JWT")
	}
	headerJSON, err := base64.RawURLEncoding.DecodeString(parts[0])
	if err != nil || len(headerJSON) > 8<<10 {
		return errors.New("DPoP proof header is invalid")
	}
	var header proofHeader
	if err := json.Unmarshal(headerJSON, &header); err != nil || header.Type != "dpop+jwt" {
		return errors.New("DPoP proof type is invalid")
	}
	expectedKeyType, ok := allowedProofAlgorithms[header.Algorithm]
	if !ok || len(header.JWK) == 0 {
		return errors.New("DPoP proof algorithm is not allowed")
	}
	for _, privateMember := range []string{"d", "p", "q", "dp", "dq", "qi", "oth", "k"} {
		if _, exists := header.JWK[privateMember]; exists {
			return errors.New("DPoP proof must contain only a public asymmetric key")
		}
	}
	var keyType string
	if err := json.Unmarshal(header.JWK["kty"], &keyType); err != nil || keyType != expectedKeyType {
		return errors.New("DPoP proof key type does not match its algorithm")
	}
	return nil
}

func unauthorized(code string) *authenticationError {
	return &authenticationError{status: http.StatusUnauthorized, code: code}
}

func durationPointer(value time.Duration) *time.Duration { return &value }
