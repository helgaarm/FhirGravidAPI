# HelseID setup

## Registrations

The facade needs:

- an inbound API registration for its configured audience and read scope;
- an actor client permitted to exchange the incoming subject token for `nhn:maternity-record` / `nhn:maternity-record/api`;
- one private asymmetric JWK for `private_key_jwt` client assertions;
- a separate private asymmetric JWK for DPoP.

The server-side test client needs its own OIDC client, audience, scope, client-assertion key, and DPoP key. Never commit keys or tokens.

## Request flow

1. The client authenticates through HelseID and calls the facade with a DPoP-bound access token.
2. The facade validates issuer, audience, lifetime, DPoP and exact read scope.
3. The facade validates a short-lived patient context bound to the current HelseID `sub`.
4. The incoming access token is used only as `subject_token` in HelseID token exchange.
5. The exchanged DHG token and a destination-bound DPoP proof are sent to DHG; the inbound token is never forwarded to DHG.

## Required configuration

See `HelseId` and `PatientContext` in the API `appsettings.json`. Supply `ClientId`, `ClientAssertionJwk`, `DPoPJwk`, and synthetic Test aliases through an approved secret/configuration provider. Startup rejects empty facade scope, missing private keys, unsupported DHG environment names, and Test/Production mixing.

## Test and production status

The test alias endpoint is non-Production only and requires the same read policy as FHIR. It binds each issued context to the authenticated subject.

Production is blocked until an approved patient-context authority and interoperability contract are implemented. That decision must cover authorization basis, issuer identity, subject/purpose binding, key storage/rotation, replay controls, audit, multi-instance Data Protection, and revocation/expiry.

An external smoke test must be explicitly opted in and use only an approved synthetic patient. The repository currently has no such credentialed smoke harness.
