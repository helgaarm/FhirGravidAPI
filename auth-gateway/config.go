package main

import (
	"fmt"
	"net"
	"net/url"
	"os"
	"strconv"
	"strings"
)

const (
	modeAuthenticate = "authenticate"
	modePassthrough  = "passthrough"
	replayMemory     = "memory"
	replayRedis      = "redis"
)

type config struct {
	listenAddress  string
	upstream       *url.URL
	mode           string
	externalScheme string
	externalHost   string
	sharedSecret   string
	authority      string
	audience       string
	requiredScope  string
	replayMode     string
	singleReplica  bool
	redisURL       string
}

func loadConfig() (config, error) {
	return loadConfigFrom(os.Getenv)
}

func loadConfigFrom(getenv func(string) string) (config, error) {
	result := config{
		listenAddress:  valueOrDefault(getenv("AUTH_GATEWAY_LISTEN_ADDR"), ":8080"),
		mode:           valueOrDefault(getenv("AUTH_GATEWAY_MODE"), modeAuthenticate),
		externalScheme: valueOrDefault(getenv("AUTH_GATEWAY_EXTERNAL_SCHEME"), "https"),
		externalHost:   getenv("AUTH_GATEWAY_EXTERNAL_HOST"),
		sharedSecret:   getenv("AUTH_GATEWAY_SHARED_SECRET"),
		authority:      strings.TrimRight(getenv("HELSEID_AUTHORITY"), "/"),
		audience:       getenv("HELSEID_AUDIENCE"),
		requiredScope:  getenv("HELSEID_SCOPE"),
		replayMode:     getenv("AUTH_GATEWAY_REPLAY_STORE"),
		redisURL:       getenv("AUTH_GATEWAY_REDIS_URL"),
	}

	upstream, err := url.Parse(valueOrDefault(getenv("AUTH_GATEWAY_UPSTREAM_URL"), "http://127.0.0.1:8081"))
	if err != nil || upstream.Scheme != "http" || upstream.Host == "" || upstream.User != nil ||
		upstream.RawQuery != "" || upstream.Fragment != "" || (upstream.Path != "" && upstream.Path != "/") ||
		!isLoopbackHost(upstream.Hostname()) {
		return config{}, fmt.Errorf("AUTH_GATEWAY_UPSTREAM_URL must be an HTTP loopback origin without a path, query, or credentials")
	}
	result.upstream = upstream

	if value := getenv("AUTH_GATEWAY_SINGLE_REPLICA"); value != "" {
		result.singleReplica, err = strconv.ParseBool(value)
		if err != nil {
			return config{}, fmt.Errorf("AUTH_GATEWAY_SINGLE_REPLICA must be true or false")
		}
	}

	if result.mode != modeAuthenticate && result.mode != modePassthrough {
		return config{}, fmt.Errorf("AUTH_GATEWAY_MODE must be %q or %q", modeAuthenticate, modePassthrough)
	}
	if result.externalScheme != "https" && !(result.mode == modePassthrough && result.externalScheme == "http") {
		return config{}, fmt.Errorf("AUTH_GATEWAY_EXTERNAL_SCHEME must be https when authentication is enabled")
	}
	if result.mode == modePassthrough {
		return result, nil
	}

	if len([]byte(result.sharedSecret)) < 32 {
		return config{}, fmt.Errorf("AUTH_GATEWAY_SHARED_SECRET must contain at least 32 bytes")
	}
	if result.audience == "" || result.requiredScope == "" {
		return config{}, fmt.Errorf("HELSEID_AUDIENCE and HELSEID_SCOPE are required")
	}
	if err := validateExternalHost(result.externalHost); err != nil {
		return config{}, err
	}
	if err := validateAuthority(result.authority); err != nil {
		return config{}, err
	}

	switch result.replayMode {
	case replayMemory:
		if !result.singleReplica {
			return config{}, fmt.Errorf("the memory replay store requires AUTH_GATEWAY_SINGLE_REPLICA=true")
		}
	case replayRedis:
		redisURL, parseErr := url.Parse(result.redisURL)
		if parseErr != nil || redisURL.Host == "" || (redisURL.Scheme != "rediss" && redisURL.Scheme != "redis") {
			return config{}, fmt.Errorf("AUTH_GATEWAY_REDIS_URL must be a redis:// or rediss:// URL")
		}
		if redisURL.Scheme != "rediss" && !isLoopbackHost(redisURL.Hostname()) {
			return config{}, fmt.Errorf("a non-loopback Redis replay store must use TLS via rediss://")
		}
	default:
		return config{}, fmt.Errorf("AUTH_GATEWAY_REPLAY_STORE must be explicitly set to %q or %q", replayMemory, replayRedis)
	}

	return result, nil
}

func validateExternalHost(value string) error {
	parsed, err := url.Parse("https://" + value)
	if err != nil || value == "" || parsed.Host != value || parsed.Hostname() == "" || parsed.User != nil ||
		parsed.Path != "" || parsed.RawQuery != "" || parsed.Fragment != "" {
		return fmt.Errorf("AUTH_GATEWAY_EXTERNAL_HOST must be the canonical public host with an optional port")
	}
	return nil
}

func validateAuthority(value string) error {
	authority, err := url.Parse(value)
	if err != nil || authority.Scheme != "https" || authority.Host == "" || authority.User != nil ||
		authority.RawQuery != "" || authority.Fragment != "" || (authority.Path != "" && authority.Path != "/") {
		return fmt.Errorf("HELSEID_AUTHORITY must be an HTTPS origin without a path, query, or credentials")
	}
	return nil
}

func isLoopbackHost(host string) bool {
	if strings.EqualFold(host, "localhost") {
		return true
	}
	address := net.ParseIP(host)
	return address != nil && address.IsLoopback()
}

func valueOrDefault(value, fallback string) string {
	if value == "" {
		return fallback
	}
	return value
}
