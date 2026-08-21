package main

import (
	"context"
	"errors"
	"log"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"
)

func main() {
	if err := run(); err != nil {
		log.Printf("auth gateway stopped: %v", err)
		os.Exit(1)
	}
}

func run() error {
	cfg, err := loadConfig()
	if err != nil {
		return err
	}

	ctx, cancel := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer cancel()

	var auth *authenticator
	var replays replayStore
	if cfg.mode == modeAuthenticate {
		tokens, err := newHelseIDTokenValidator(ctx, cfg.authority, cfg.audience)
		if err != nil {
			return err
		}
		replays, err = newReplayStore(ctx, cfg)
		if err != nil {
			return err
		}
		defer replays.close()
		auth = newAuthenticator(tokens, replays, cfg.externalScheme, cfg.externalHost, cfg.requiredScope)
	} else {
		log.Print("auth gateway is running in explicit passthrough mode")
	}

	server := &http.Server{
		Addr:              cfg.listenAddress,
		Handler:           newGatewayHandler(cfg, auth),
		ReadTimeout:       15 * time.Second,
		ReadHeaderTimeout: 5 * time.Second,
		WriteTimeout:      60 * time.Second,
		IdleTimeout:       60 * time.Second,
		MaxHeaderBytes:    128 << 10,
	}

	serverErrors := make(chan error, 1)
	go func() {
		log.Printf("auth gateway listening in %s mode", cfg.mode)
		serverErrors <- server.ListenAndServe()
	}()

	select {
	case <-ctx.Done():
		shutdownContext, shutdownCancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer shutdownCancel()
		return server.Shutdown(shutdownContext)
	case err := <-serverErrors:
		if errors.Is(err, http.ErrServerClosed) {
			return nil
		}
		return err
	}
}
