package main

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/url"
	"strings"
	"time"

	dpop "github.com/AxisCommunications/go-dpop"
	"github.com/MicahParks/keyfunc/v3"
	"github.com/golang-jwt/jwt/v5"
)

const discoveryResponseLimit = 1 << 20

type scopeClaim []string

func (claim *scopeClaim) UnmarshalJSON(data []byte) error {
	if strings.TrimSpace(string(data)) == "null" {
		return errors.New("scope must be a string or an array of strings")
	}
	var text string
	if err := json.Unmarshal(data, &text); err == nil {
		*claim = strings.Fields(text)
		return nil
	}

	var values []string
	if err := json.Unmarshal(data, &values); err != nil || values == nil {
		return errors.New("scope must be a string or an array of strings")
	}
	for _, value := range values {
		if strings.TrimSpace(value) == "" {
			return errors.New("scope array values must be non-empty strings")
		}
	}
	*claim = values
	return nil
}

func (claim scopeClaim) contains(required string) bool {
	for _, value := range claim {
		if value == required {
			return true
		}
	}
	return false
}

type accessTokenClaims struct {
	dpop.BoundAccessTokenClaims
	Scope scopeClaim `json:"scope"`
}

type accessTokenValidator interface {
	validate(context.Context, string) (*jwt.Token, *accessTokenClaims, error)
}

type helseIDTokenValidator struct {
	issuer   string
	audience string
	keyFunc  jwt.Keyfunc
}

func (validator *helseIDTokenValidator) validate(
	ctx context.Context,
	raw string,
) (*jwt.Token, *accessTokenClaims, error) {
	claims := &accessTokenClaims{
		BoundAccessTokenClaims: dpop.BoundAccessTokenClaims{
			RegisteredClaims: &jwt.RegisteredClaims{},
		},
	}
	token, err := jwt.ParseWithClaims(
		raw,
		claims,
		func(token *jwt.Token) (any, error) {
			if err := ctx.Err(); err != nil {
				return nil, err
			}
			return validator.keyFunc(token)
		},
		jwt.WithValidMethods([]string{"RS256", "PS256"}),
		jwt.WithIssuer(validator.issuer),
		jwt.WithAudience(validator.audience),
		jwt.WithExpirationRequired(),
		jwt.WithLeeway(3*time.Second),
		jwt.WithStrictDecoding(),
	)
	if err != nil || token == nil || !token.Valid {
		return nil, nil, errors.New("access token validation failed")
	}
	if tokenType, ok := token.Header["typ"].(string); !ok || tokenType != "at+jwt" {
		return nil, nil, errors.New("access token has an invalid type")
	}
	if claims.NotBefore == nil {
		return nil, nil, errors.New("access token has no not-before claim")
	}
	if len(claims.Audience) != 1 || claims.Audience[0] != validator.audience {
		return nil, nil, errors.New("access token must have exactly the configured audience")
	}
	return token, claims, nil
}

type discoveryDocument struct {
	Issuer  string `json:"issuer"`
	JWKSURL string `json:"jwks_uri"`
}

func newHelseIDTokenValidator(
	ctx context.Context,
	authority string,
	audience string,
) (*helseIDTokenValidator, error) {
	client := hardenedHTTPClient()
	discovery, err := fetchDiscovery(ctx, client, authority)
	if err != nil {
		return nil, err
	}

	keys, err := keyfunc.NewDefaultOverrideCtx(ctx, []string{discovery.JWKSURL}, keyfunc.Override{
		Client:      client,
		HTTPTimeout: 10 * time.Second,
	})
	if err != nil {
		return nil, fmt.Errorf("initialize HelseID signing keys: %w", err)
	}

	return &helseIDTokenValidator{
		issuer:   discovery.Issuer,
		audience: audience,
		keyFunc:  keys.Keyfunc,
	}, nil
}

func fetchDiscovery(ctx context.Context, client *http.Client, authority string) (discoveryDocument, error) {
	authorityURL, _ := url.Parse(authority)
	discoveryURL := authorityURL.ResolveReference(&url.URL{Path: "/.well-known/openid-configuration"})
	request, err := http.NewRequestWithContext(ctx, http.MethodGet, discoveryURL.String(), nil)
	if err != nil {
		return discoveryDocument{}, fmt.Errorf("create HelseID discovery request: %w", err)
	}
	request.Header.Set("Accept", "application/json")
	request.Header.Set("User-Agent", "population-data-facade-auth-gateway/1.0")

	response, err := client.Do(request)
	if err != nil {
		return discoveryDocument{}, fmt.Errorf("retrieve HelseID discovery document: %w", err)
	}
	defer response.Body.Close()
	if response.StatusCode != http.StatusOK {
		return discoveryDocument{}, fmt.Errorf("retrieve HelseID discovery document: unexpected HTTP status %d", response.StatusCode)
	}

	body, err := io.ReadAll(io.LimitReader(response.Body, discoveryResponseLimit+1))
	if err != nil {
		return discoveryDocument{}, fmt.Errorf("read HelseID discovery document: %w", err)
	}
	if len(body) > discoveryResponseLimit {
		return discoveryDocument{}, errors.New("HelseID discovery document exceeds the size limit")
	}

	var document discoveryDocument
	decoder := json.NewDecoder(strings.NewReader(string(body)))
	if err := decoder.Decode(&document); err != nil {
		return discoveryDocument{}, fmt.Errorf("decode HelseID discovery document: %w", err)
	}
	if err := ensureJSONEnd(decoder); err != nil {
		return discoveryDocument{}, err
	}
	if document.Issuer != authority {
		return discoveryDocument{}, errors.New("HelseID discovery issuer does not exactly match HELSEID_AUTHORITY")
	}

	jwksURL, err := url.Parse(document.JWKSURL)
	if err != nil || jwksURL.Scheme != "https" || jwksURL.Host == "" || jwksURL.User != nil || jwksURL.Fragment != "" ||
		!strings.EqualFold(jwksURL.Host, authorityURL.Host) {
		return discoveryDocument{}, errors.New("HelseID jwks_uri must be HTTPS and use the configured authority host")
	}
	return document, nil
}

func ensureJSONEnd(decoder *json.Decoder) error {
	var extra any
	if err := decoder.Decode(&extra); !errors.Is(err, io.EOF) {
		if err == nil {
			return errors.New("HelseID discovery response contains multiple JSON values")
		}
		return fmt.Errorf("decode HelseID discovery document: %w", err)
	}
	return nil
}

func hardenedHTTPClient() *http.Client {
	return &http.Client{
		Timeout: 10 * time.Second,
		Transport: &http.Transport{
			Proxy:                 http.ProxyFromEnvironment,
			DialContext:           (&net.Dialer{Timeout: 5 * time.Second, KeepAlive: 30 * time.Second}).DialContext,
			ForceAttemptHTTP2:     true,
			MaxIdleConns:          20,
			IdleConnTimeout:       60 * time.Second,
			TLSHandshakeTimeout:   5 * time.Second,
			ResponseHeaderTimeout: 5 * time.Second,
		},
	}
}
