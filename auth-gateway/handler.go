package main

import (
	"bytes"
	"encoding/json"
	"errors"
	"io"
	"log"
	"net/http"
	"net/http/httputil"
	"net/url"
	"time"
)

const (
	gatewaySecretHeader     = "X-Auth-Gateway-Secret"
	maximumRequestBodyBytes = 1 << 20
)

type gatewayHandler struct {
	mode        string
	auth        *authenticator
	proxy       *httputil.ReverseProxy
	upstream    *url.URL
	readyClient *http.Client
}

func newGatewayHandler(cfg config, auth *authenticator) *gatewayHandler {
	proxy := httputil.NewSingleHostReverseProxy(cfg.upstream)
	originalDirector := proxy.Director
	proxy.Director = func(request *http.Request) {
		originalHost := request.Host
		request.Header.Del(gatewaySecretHeader)
		request.Header.Del("Forwarded")
		request.Header.Del("X-Forwarded-For")
		request.Header.Del("X-Forwarded-Host")
		request.Header.Del("X-Forwarded-Proto")
		originalDirector(request)
		request.Header.Set("X-Forwarded-Host", originalHost)
		request.Header.Set("X-Forwarded-Proto", cfg.externalScheme)
		if cfg.sharedSecret != "" {
			request.Header.Set(gatewaySecretHeader, cfg.sharedSecret)
		}
	}
	proxy.ErrorHandler = func(response http.ResponseWriter, _ *http.Request, err error) {
		var bodyTooLarge *http.MaxBytesError
		if errors.As(err, &bodyTooLarge) {
			writeOperationOutcome(response, http.StatusRequestEntityTooLarge, "too-costly", "The request body exceeds the gateway limit.")
			return
		}
		log.Print("auth gateway could not reach the private API")
		writeOperationOutcome(response, http.StatusBadGateway, "exception", "The private API is unavailable.")
	}

	return &gatewayHandler{
		mode:        cfg.mode,
		auth:        auth,
		proxy:       proxy,
		upstream:    cfg.upstream,
		readyClient: &http.Client{Timeout: 3 * time.Second},
	}
}

func (handler *gatewayHandler) ServeHTTP(response http.ResponseWriter, request *http.Request) {
	if !allowedGatewayMethod(request.Method) {
		response.Header().Set("Allow", "GET, HEAD, POST")
		writeOperationOutcome(response, http.StatusMethodNotAllowed, "not-supported", "The HTTP method is not supported by this facade.")
		return
	}
	if request.ContentLength > maximumRequestBodyBytes {
		writeOperationOutcome(response, http.StatusRequestEntityTooLarge, "too-costly", "The request body exceeds the gateway limit.")
		return
	}
	if request.Body != nil {
		request.Body = http.MaxBytesReader(response, request.Body, maximumRequestBodyBytes)
		body, err := io.ReadAll(request.Body)
		if err != nil {
			var bodyTooLarge *http.MaxBytesError
			if errors.As(err, &bodyTooLarge) {
				writeOperationOutcome(response, http.StatusRequestEntityTooLarge, "too-costly", "The request body exceeds the gateway limit.")
				return
			}
			writeOperationOutcome(response, http.StatusBadRequest, "invalid", "The request body could not be read.")
			return
		}
		request.Body = io.NopCloser(bytes.NewReader(body))
		request.ContentLength = int64(len(body))
	}

	switch request.URL.Path {
	case "/health/live", "/health/ready":
		if request.Method != http.MethodGet && request.Method != http.MethodHead {
			response.Header().Set("Allow", "GET, HEAD")
			writeOperationOutcome(response, http.StatusMethodNotAllowed, "not-supported", "The HTTP method is not supported by this endpoint.")
			return
		}
		if request.URL.Path == "/health/live" {
			writeHealth(response, http.StatusOK)
			return
		}
		handler.writeReadiness(response, request)
		return
	}

	if handler.mode == modeAuthenticate {
		if err := handler.auth.authenticate(request); err != nil {
			authError, ok := err.(*authenticationError)
			if !ok {
				authError = unauthorized("authentication_failed")
			}
			log.Printf("auth gateway rejected a request: %s", authError.code)
			if authError.status == http.StatusUnauthorized {
				response.Header().Set("WWW-Authenticate", "DPoP")
			}
			response.Header().Set("Cache-Control", "no-store")
			message := "A valid HelseID DPoP access token and proof are required."
			issueCode := "security"
			if authError.status == http.StatusForbidden {
				message = "The token does not grant access to this operation."
				issueCode = "forbidden"
			}
			writeOperationOutcome(response, authError.status, issueCode, message)
			return
		}
	}

	handler.proxy.ServeHTTP(response, request)
}

func allowedGatewayMethod(method string) bool {
	return method == http.MethodGet || method == http.MethodHead || method == http.MethodPost
}

func (handler *gatewayHandler) writeReadiness(response http.ResponseWriter, request *http.Request) {
	target := handler.upstream.ResolveReference(&url.URL{Path: "/health/ready"})
	probe, err := http.NewRequestWithContext(request.Context(), http.MethodGet, target.String(), nil)
	if err != nil {
		writeHealth(response, http.StatusServiceUnavailable)
		return
	}
	upstreamResponse, err := handler.readyClient.Do(probe)
	if err != nil {
		writeHealth(response, http.StatusServiceUnavailable)
		return
	}
	defer upstreamResponse.Body.Close()
	if upstreamResponse.StatusCode < 200 || upstreamResponse.StatusCode >= 300 {
		writeHealth(response, http.StatusServiceUnavailable)
		return
	}
	writeHealth(response, http.StatusOK)
}

func writeHealth(response http.ResponseWriter, status int) {
	response.Header().Set("Content-Type", "application/json")
	response.Header().Set("Cache-Control", "no-store")
	response.WriteHeader(status)
	_ = json.NewEncoder(response).Encode(map[string]string{"status": http.StatusText(status)})
}

func writeOperationOutcome(response http.ResponseWriter, status int, issueCode, message string) {
	response.Header().Set("Content-Type", "application/fhir+json")
	response.Header().Set("Cache-Control", "no-store")
	response.WriteHeader(status)
	_ = json.NewEncoder(response).Encode(map[string]any{
		"resourceType": "OperationOutcome",
		"issue": []map[string]string{{
			"severity":    "error",
			"code":        issueCode,
			"diagnostics": message,
		}},
	})
}
