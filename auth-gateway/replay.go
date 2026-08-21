package main

import (
	"context"
	"errors"
	"fmt"
	"sync"
	"time"

	"github.com/redis/go-redis/v9"
)

const maximumReplayEntries = 100_000

type replayStore interface {
	use(context.Context, string, time.Time) (bool, error)
	close() error
}

type memoryReplayStore struct {
	mu      sync.Mutex
	entries map[string]time.Time
}

func newMemoryReplayStore() *memoryReplayStore {
	return &memoryReplayStore{entries: make(map[string]time.Time)}
}

func (store *memoryReplayStore) use(_ context.Context, key string, expiresAt time.Time) (bool, error) {
	store.mu.Lock()
	defer store.mu.Unlock()

	now := time.Now()
	for candidate, expiry := range store.entries {
		if !expiry.After(now) {
			delete(store.entries, candidate)
		}
	}
	if expiry, exists := store.entries[key]; exists && expiry.After(now) {
		return false, nil
	}
	if len(store.entries) >= maximumReplayEntries {
		return false, errors.New("replay store capacity reached")
	}
	store.entries[key] = expiresAt
	return true, nil
}

func (*memoryReplayStore) close() error { return nil }

type redisReplayStore struct {
	client *redis.Client
}

func newRedisReplayStore(ctx context.Context, rawURL string) (*redisReplayStore, error) {
	options, err := redis.ParseURL(rawURL)
	if err != nil {
		return nil, errors.New("parse Redis replay store URL")
	}
	client := redis.NewClient(options)
	pingContext, cancel := context.WithTimeout(ctx, 5*time.Second)
	defer cancel()
	if err := client.Ping(pingContext).Err(); err != nil {
		_ = client.Close()
		return nil, fmt.Errorf("connect to Redis replay store: %w", err)
	}
	return &redisReplayStore{client: client}, nil
}

func (store *redisReplayStore) use(ctx context.Context, key string, expiresAt time.Time) (bool, error) {
	ttl := time.Until(expiresAt)
	if ttl <= 0 {
		ttl = time.Second
	}
	return store.client.SetNX(ctx, "dpop:replay:"+key, "1", ttl).Result()
}

func (store *redisReplayStore) close() error { return store.client.Close() }

func newReplayStore(ctx context.Context, cfg config) (replayStore, error) {
	switch cfg.replayMode {
	case replayMemory:
		return newMemoryReplayStore(), nil
	case replayRedis:
		return newRedisReplayStore(ctx, cfg.redisURL)
	default:
		return nil, errors.New("no replay store configured")
	}
}
