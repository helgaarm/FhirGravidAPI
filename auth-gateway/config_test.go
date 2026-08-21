package main

import (
	"strings"
	"testing"
)

func TestValidAuthenticationConfiguration(t *testing.T) {
	result, err := loadConfigFrom(environmentGetter(validEnvironment()))

	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if result.upstream.String() != "http://127.0.0.1:8081" || result.mode != modeAuthenticate {
		t.Fatalf("unexpected configuration: %#v", result)
	}
}

func TestAuthenticationConfigRequiresExplicitReplayTopology(t *testing.T) {
	values := validEnvironment()
	delete(values, "AUTH_GATEWAY_REPLAY_STORE")

	_, err := loadConfigFrom(func(name string) string { return values[name] })

	if err == nil || !strings.Contains(err.Error(), "REPLAY_STORE") {
		t.Fatalf("expected replay store validation error, got %v", err)
	}
}

func TestMemoryReplayStoreRequiresSingleReplica(t *testing.T) {
	values := validEnvironment()
	values["AUTH_GATEWAY_SINGLE_REPLICA"] = "false"

	_, err := loadConfigFrom(func(name string) string { return values[name] })

	if err == nil || !strings.Contains(err.Error(), "SINGLE_REPLICA") {
		t.Fatalf("expected single-replica validation error, got %v", err)
	}
}

func TestPassthroughModeDoesNotRequireHelseIDSecrets(t *testing.T) {
	values := map[string]string{
		"AUTH_GATEWAY_MODE":            modePassthrough,
		"AUTH_GATEWAY_EXTERNAL_SCHEME": "https",
	}

	result, err := loadConfigFrom(func(name string) string { return values[name] })

	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if result.mode != modePassthrough {
		t.Fatalf("expected passthrough mode, got %q", result.mode)
	}
}

func TestNonLoopbackRedisMustUseTLS(t *testing.T) {
	values := validEnvironment()
	values["AUTH_GATEWAY_REPLAY_STORE"] = replayRedis
	values["AUTH_GATEWAY_REDIS_URL"] = "redis://cache.example.test:6379/0"

	_, err := loadConfigFrom(func(name string) string { return values[name] })

	if err == nil || !strings.Contains(err.Error(), "rediss") {
		t.Fatalf("expected Redis TLS validation error, got %v", err)
	}
}

func TestUpstreamMustRemainAnHTTPLoopbackOrigin(t *testing.T) {
	invalid := []string{
		"https://127.0.0.1:8081",
		"http://api.example.test:8081",
		"http://user:password@127.0.0.1:8081",
		"http://127.0.0.1:8081/fhir",
		"http://127.0.0.1:8081?target=other",
		"http://127.0.0.1:8081#fragment",
	}
	for _, upstream := range invalid {
		t.Run(upstream, func(t *testing.T) {
			values := validEnvironment()
			values["AUTH_GATEWAY_UPSTREAM_URL"] = upstream

			_, err := loadConfigFrom(environmentGetter(values))

			if err == nil || !strings.Contains(err.Error(), "loopback origin") {
				t.Fatalf("expected loopback-origin error for %q, got %v", upstream, err)
			}
		})
	}
}

func TestExpectedLoopbackUpstreamsAreAccepted(t *testing.T) {
	for _, upstream := range []string{
		"http://localhost:8081",
		"http://127.0.0.1:8081",
		"http://[::1]:8081",
	} {
		t.Run(upstream, func(t *testing.T) {
			values := validEnvironment()
			values["AUTH_GATEWAY_UPSTREAM_URL"] = upstream

			if _, err := loadConfigFrom(environmentGetter(values)); err != nil {
				t.Fatalf("expected %q to be accepted, got %v", upstream, err)
			}
		})
	}
}

func TestPassthroughStillRequiresALoopbackUpstream(t *testing.T) {
	values := map[string]string{
		"AUTH_GATEWAY_MODE":            modePassthrough,
		"AUTH_GATEWAY_EXTERNAL_SCHEME": "http",
		"AUTH_GATEWAY_UPSTREAM_URL":    "http://api.example.test:8081",
	}

	_, err := loadConfigFrom(environmentGetter(values))

	if err == nil || !strings.Contains(err.Error(), "loopback origin") {
		t.Fatalf("expected loopback-origin error, got %v", err)
	}
}

func TestAuthenticationConfigurationRejectsInvalidSecuritySettings(t *testing.T) {
	tests := []struct {
		name     string
		mutate   func(map[string]string)
		expected string
	}{
		{"mode", func(values map[string]string) { values["AUTH_GATEWAY_MODE"] = "disabled" }, "AUTH_GATEWAY_MODE"},
		{"external scheme", func(values map[string]string) { values["AUTH_GATEWAY_EXTERNAL_SCHEME"] = "http" }, "EXTERNAL_SCHEME"},
		{"short secret", func(values map[string]string) { values["AUTH_GATEWAY_SHARED_SECRET"] = "short" }, "SHARED_SECRET"},
		{"audience", func(values map[string]string) { delete(values, "HELSEID_AUDIENCE") }, "AUDIENCE"},
		{"scope", func(values map[string]string) { delete(values, "HELSEID_SCOPE") }, "SCOPE"},
		{"external host", func(values map[string]string) { values["AUTH_GATEWAY_EXTERNAL_HOST"] = "https://facade.example/path" }, "EXTERNAL_HOST"},
		{"authority scheme", func(values map[string]string) { values["HELSEID_AUTHORITY"] = "http://helseid.example" }, "HELSEID_AUTHORITY"},
		{"authority path", func(values map[string]string) { values["HELSEID_AUTHORITY"] = "https://helseid.example/path" }, "HELSEID_AUTHORITY"},
		{"single replica", func(values map[string]string) { values["AUTH_GATEWAY_SINGLE_REPLICA"] = "sometimes" }, "SINGLE_REPLICA"},
	}
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			values := validEnvironment()
			test.mutate(values)

			_, err := loadConfigFrom(environmentGetter(values))

			if err == nil || !strings.Contains(err.Error(), test.expected) {
				t.Fatalf("expected %s validation error, got %v", test.expected, err)
			}
		})
	}
}

func TestRedisConfigurationRequiresAValidURL(t *testing.T) {
	for _, rawURL := range []string{"", "https://cache.example", "redis://"} {
		t.Run(rawURL, func(t *testing.T) {
			values := validEnvironment()
			values["AUTH_GATEWAY_REPLAY_STORE"] = replayRedis
			values["AUTH_GATEWAY_REDIS_URL"] = rawURL

			_, err := loadConfigFrom(environmentGetter(values))

			if err == nil || !strings.Contains(err.Error(), "REDIS_URL") {
				t.Fatalf("expected Redis URL validation error, got %v", err)
			}
		})
	}
}

func environmentGetter(values map[string]string) func(string) string {
	return func(name string) string { return values[name] }
}

func validEnvironment() map[string]string {
	return map[string]string{
		"AUTH_GATEWAY_MODE":           modeAuthenticate,
		"AUTH_GATEWAY_SHARED_SECRET":  strings.Repeat("s", 32),
		"AUTH_GATEWAY_REPLAY_STORE":   replayMemory,
		"AUTH_GATEWAY_SINGLE_REPLICA": "true",
		"AUTH_GATEWAY_EXTERNAL_HOST":  "facade.example",
		"HELSEID_AUTHORITY":           "https://helseid-sts.test.nhn.no",
		"HELSEID_AUDIENCE":            "nhn:population-data-facade",
		"HELSEID_SCOPE":               "nhn:population-data-facade/read",
	}
}
