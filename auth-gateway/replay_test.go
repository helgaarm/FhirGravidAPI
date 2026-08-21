package main

import (
	"context"
	"fmt"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	miniredis "github.com/alicebob/miniredis/v2"
)

func TestMemoryReplayStoreAcceptsAKeyExactlyOnceUnderConcurrency(t *testing.T) {
	store := newMemoryReplayStore()
	var accepted atomic.Int32
	var wait sync.WaitGroup
	for range 64 {
		wait.Add(1)
		go func() {
			defer wait.Done()
			ok, err := store.use(context.Background(), "same-proof", time.Now().Add(time.Minute))
			if err != nil {
				t.Errorf("unexpected error: %v", err)
				return
			}
			if ok {
				accepted.Add(1)
			}
		}()
	}
	wait.Wait()
	if accepted.Load() != 1 {
		t.Fatalf("expected exactly one acceptance, got %d", accepted.Load())
	}
}

func TestMemoryReplayStoreRemovesExpiredEntries(t *testing.T) {
	store := newMemoryReplayStore()
	store.entries["expired-proof"] = time.Now().Add(-time.Minute)

	accepted, err := store.use(context.Background(), "expired-proof", time.Now().Add(time.Minute))

	if err != nil || !accepted {
		t.Fatalf("expected an expired key to be accepted again, accepted=%v err=%v", accepted, err)
	}
}

func TestMemoryReplayStoreFailsClosedAtCapacity(t *testing.T) {
	store := newMemoryReplayStore()
	expiresAt := time.Now().Add(time.Hour)
	for index := range maximumReplayEntries {
		store.entries[fmt.Sprintf("proof-%d", index)] = expiresAt
	}

	accepted, err := store.use(context.Background(), "new-proof", expiresAt)

	if err == nil || accepted || !strings.Contains(err.Error(), "capacity") {
		t.Fatalf("expected capacity failure, accepted=%v err=%v", accepted, err)
	}
	if accepted, err = store.use(context.Background(), "proof-1", expiresAt); err != nil || accepted {
		t.Fatalf("expected an existing live key to remain rejected, accepted=%v err=%v", accepted, err)
	}
}

func TestRedisReplayStoreUsesAtomicSetWithExpiry(t *testing.T) {
	server := miniredis.RunT(t)
	store, err := newRedisReplayStore(context.Background(), "redis://"+server.Addr())
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = store.close() })

	expiresAt := time.Now().Add(30 * time.Second)
	accepted, err := store.use(context.Background(), "redis-proof", expiresAt)
	if err != nil || !accepted {
		t.Fatalf("expected first use to succeed, accepted=%v err=%v", accepted, err)
	}
	accepted, err = store.use(context.Background(), "redis-proof", expiresAt)
	if err != nil || accepted {
		t.Fatalf("expected replay to be rejected, accepted=%v err=%v", accepted, err)
	}
	if ttl := server.TTL("dpop:replay:redis-proof"); ttl <= 0 || ttl > 30*time.Second {
		t.Fatalf("unexpected replay TTL %v", ttl)
	}
}

func TestRedisReplayStoreClampsExpiredTTLAndFailsClosed(t *testing.T) {
	server := miniredis.RunT(t)
	store, err := newRedisReplayStore(context.Background(), "redis://"+server.Addr())
	if err != nil {
		t.Fatal(err)
	}

	accepted, err := store.use(context.Background(), "expired-proof", time.Now().Add(-time.Minute))
	if err != nil || !accepted {
		t.Fatalf("expected clamped first use, accepted=%v err=%v", accepted, err)
	}
	if ttl := server.TTL("dpop:replay:expired-proof"); ttl != time.Second {
		t.Fatalf("expected one-second TTL, got %v", ttl)
	}
	server.Close()
	if accepted, err = store.use(context.Background(), "another-proof", time.Now().Add(time.Minute)); err == nil || accepted {
		t.Fatalf("expected Redis failure to fail closed, accepted=%v err=%v", accepted, err)
	}
	_ = store.close()
}

func TestReplayStoreFactorySelectsConfiguredImplementation(t *testing.T) {
	memory, err := newReplayStore(context.Background(), config{replayMode: replayMemory})
	if err != nil {
		t.Fatal(err)
	}
	if _, ok := memory.(*memoryReplayStore); !ok {
		t.Fatalf("expected memory replay store, got %T", memory)
	}

	if _, err := newReplayStore(context.Background(), config{}); err == nil {
		t.Fatal("expected missing replay configuration to fail")
	}
}
