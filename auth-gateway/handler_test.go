package main

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strings"
	"sync/atomic"
	"testing"
)

func TestGatewayRebuildsForwardingHeadersAndPreservesTheRequestTarget(t *testing.T) {
	type capturedRequest struct {
		path           string
		rawPath        string
		rawQuery       string
		authorization  string
		dpop           string
		forwarded      string
		forwardedFor   string
		forwardedHost  string
		forwardedProto string
	}
	captured := make(chan capturedRequest, 1)
	upstream := httptest.NewServer(http.HandlerFunc(func(response http.ResponseWriter, request *http.Request) {
		captured <- capturedRequest{
			path: request.URL.Path, rawPath: request.URL.RawPath, rawQuery: request.URL.RawQuery,
			authorization: request.Header.Get("Authorization"), dpop: request.Header.Get("DPoP"),
			forwarded: request.Header.Get("Forwarded"), forwardedFor: request.Header.Get("X-Forwarded-For"),
			forwardedHost: request.Header.Get("X-Forwarded-Host"), forwardedProto: request.Header.Get("X-Forwarded-Proto"),
		}
		response.WriteHeader(http.StatusNoContent)
	}))
	defer upstream.Close()
	upstreamURL, _ := url.Parse(upstream.URL)
	gateway := newGatewayHandler(config{mode: modePassthrough, upstream: upstreamURL, externalScheme: "https"}, nil)
	request := httptest.NewRequest(http.MethodGet, "https://public.example/fhir/Observation%2Fencoded?code=a%2Fb", nil)
	request.Header.Set("Authorization", "DPoP access-token")
	request.Header.Set("DPoP", "proof")
	request.Header.Set("Forwarded", "for=attacker")
	request.Header.Set("X-Forwarded-For", "192.0.2.10")
	request.Header.Set("X-Forwarded-Host", "attacker.example")
	request.Header.Set("X-Forwarded-Proto", "http")
	response := httptest.NewRecorder()

	gateway.ServeHTTP(response, request)

	if response.Code != http.StatusNoContent {
		t.Fatalf("expected 204, got %d: %s", response.Code, response.Body.String())
	}
	got := <-captured
	if got.path != "/fhir/Observation/encoded" || got.rawPath != "/fhir/Observation%2Fencoded" || got.rawQuery != "code=a%2Fb" {
		t.Fatalf("request target changed: %#v", got)
	}
	if got.authorization != "DPoP access-token" || got.dpop != "proof" {
		t.Fatalf("authorization headers changed: %#v", got)
	}
	if got.forwarded != "" || got.forwardedFor != "192.0.2.1" || got.forwardedHost != "public.example" || got.forwardedProto != "https" {
		t.Fatalf("forwarding headers were not sanitized: %#v", got)
	}
}

func TestGatewayRejectsUnsupportedMethodsAndOversizedBodies(t *testing.T) {
	var calls atomic.Int32
	upstream := httptest.NewServer(http.HandlerFunc(func(response http.ResponseWriter, _ *http.Request) {
		calls.Add(1)
		response.WriteHeader(http.StatusNoContent)
	}))
	defer upstream.Close()
	upstreamURL, _ := url.Parse(upstream.URL)
	gateway := newGatewayHandler(config{mode: modePassthrough, upstream: upstreamURL, externalScheme: "https"}, nil)

	unsupported := httptest.NewRequest(http.MethodPut, "https://public.example/fhir/Patient/1", nil)
	unsupportedResponse := httptest.NewRecorder()
	gateway.ServeHTTP(unsupportedResponse, unsupported)
	if unsupportedResponse.Code != http.StatusMethodNotAllowed || unsupportedResponse.Header().Get("Allow") != "GET, HEAD, POST" {
		t.Fatalf("unexpected method response: %d %#v", unsupportedResponse.Code, unsupportedResponse.Header())
	}

	oversized := httptest.NewRequest(http.MethodPost, "https://public.example/test/patient-context/synthetic", bytes.NewReader(make([]byte, maximumRequestBodyBytes+1)))
	oversizedResponse := httptest.NewRecorder()
	gateway.ServeHTTP(oversizedResponse, oversized)
	if oversizedResponse.Code != http.StatusRequestEntityTooLarge {
		t.Fatalf("expected 413, got %d: %s", oversizedResponse.Code, oversizedResponse.Body.String())
	}
	assertOperationOutcome(t, oversizedResponse, "too-costly")
	if calls.Load() != 0 {
		t.Fatalf("upstream was called %d times", calls.Load())
	}

	chunked := httptest.NewRequest(http.MethodPost, "https://public.example/test/patient-context/synthetic", bytes.NewReader(make([]byte, maximumRequestBodyBytes+1)))
	chunked.ContentLength = -1
	chunkedResponse := httptest.NewRecorder()
	gateway.ServeHTTP(chunkedResponse, chunked)
	if chunkedResponse.Code != http.StatusRequestEntityTooLarge {
		t.Fatalf("expected chunked oversized body to return 413, got %d: %s", chunkedResponse.Code, chunkedResponse.Body.String())
	}
	assertOperationOutcome(t, chunkedResponse, "too-costly")
}

func TestGatewayHealthEndpointsAndProxyFailureContract(t *testing.T) {
	readyStatus := http.StatusNoContent
	upstream := httptest.NewServer(http.HandlerFunc(func(response http.ResponseWriter, request *http.Request) {
		if request.URL.Path == "/health/ready" {
			response.WriteHeader(readyStatus)
			return
		}
		response.WriteHeader(http.StatusNoContent)
	}))
	upstreamURL, _ := url.Parse(upstream.URL)
	gateway := newGatewayHandler(config{mode: modePassthrough, upstream: upstreamURL, externalScheme: "https"}, nil)

	for path, expected := range map[string]int{"/health/live": http.StatusOK, "/health/ready": http.StatusOK} {
		response := httptest.NewRecorder()
		gateway.ServeHTTP(response, httptest.NewRequest(http.MethodGet, "https://public.example"+path, nil))
		if response.Code != expected || response.Header().Get("Cache-Control") != "no-store" {
			t.Fatalf("%s: expected %d no-store, got %d %#v", path, expected, response.Code, response.Header())
		}
	}
	invalidHealthMethod := httptest.NewRecorder()
	gateway.ServeHTTP(invalidHealthMethod, httptest.NewRequest(http.MethodPost, "https://public.example/health/live", nil))
	if invalidHealthMethod.Code != http.StatusMethodNotAllowed || invalidHealthMethod.Header().Get("Allow") != "GET, HEAD" {
		t.Fatalf("health endpoint accepted POST: %d %#v", invalidHealthMethod.Code, invalidHealthMethod.Header())
	}

	readyStatus = http.StatusServiceUnavailable
	notReady := httptest.NewRecorder()
	gateway.ServeHTTP(notReady, httptest.NewRequest(http.MethodGet, "https://public.example/health/ready", nil))
	if notReady.Code != http.StatusServiceUnavailable {
		t.Fatalf("expected 503, got %d", notReady.Code)
	}
	upstream.Close()

	proxyFailure := httptest.NewRecorder()
	gateway.ServeHTTP(proxyFailure, httptest.NewRequest(http.MethodGet, "https://public.example/fhir/metadata", nil))
	if proxyFailure.Code != http.StatusBadGateway || proxyFailure.Header().Get("Content-Type") != "application/fhir+json" {
		t.Fatalf("unexpected proxy failure: %d %#v", proxyFailure.Code, proxyFailure.Header())
	}
	assertOperationOutcome(t, proxyFailure, "exception")

	notReady = httptest.NewRecorder()
	gateway.ServeHTTP(notReady, httptest.NewRequest(http.MethodGet, "https://public.example/health/ready", nil))
	if notReady.Code != http.StatusServiceUnavailable {
		t.Fatalf("expected network readiness failure to return 503, got %d", notReady.Code)
	}
}

func assertOperationOutcome(t *testing.T, response *httptest.ResponseRecorder, expectedCode string) {
	t.Helper()
	if response.Header().Get("Content-Type") != "application/fhir+json" || response.Header().Get("Cache-Control") != "no-store" {
		t.Fatalf("unexpected FHIR error headers: %#v", response.Header())
	}
	var body struct {
		ResourceType string `json:"resourceType"`
		Issue        []struct {
			Code string `json:"code"`
		} `json:"issue"`
	}
	if err := json.NewDecoder(strings.NewReader(response.Body.String())).Decode(&body); err != nil {
		t.Fatalf("decode OperationOutcome: %v", err)
	}
	if body.ResourceType != "OperationOutcome" || len(body.Issue) != 1 || body.Issue[0].Code != expectedCode {
		t.Fatalf("unexpected OperationOutcome: %#v", body)
	}
}
