# HelseID setup

## Registrations

The facade needs:

- an inbound API registration for its configured audience and read scope;
- an actor client permitted to exchange the incoming subject token for `nhn:maternity-record` / `nhn:maternity-record/api`;
- one private asymmetric JWK for `private_key_jwt` client assertions;
- a separate private asymmetric JWK for DPoP.

## Request flow

1. The client authenticates through HelseID and calls the facade with a DPoP-bound access token.
2. The facade validates issuer, audience, lifetime, DPoP and exact read scope.
3. The facade validates a short-lived patient context bound to the current HelseID `sub`.
4. The incoming access token is used only as `subject_token` in HelseID token exchange.
5. The exchanged DHG token and a destination-bound DPoP proof are sent to DHG; the inbound token is never forwarded to DHG.

For local Swagger testing, `DevelopmentTestMode:Enabled=true` replaces steps 1-4 with an anonymous inbound request and a server-side HelseID `client_credentials` request for the configured DHG resource and scope. The facade still sends a DPoP-bound HelseID token to DHG. The normal mode requires host environment `Development`, `Dhg:Environment=Test`, loopback-only listeners, and a known loopback peer; do not expose that variant through a reverse proxy, tunnel, or port forwarding.

The repository's [Azure test deployment](azure-test-deployment.md) is the only supported remote exception. It explicitly sets `DevelopmentTestMode:AllowRemoteStaging=true` in `Staging`, keeps DHG in Test, and requires a trusted Container Apps ingress CIDR. It is still anonymous on the facade side and still requires server-side HelseID credentials for DHG. This exception is rejected when either host or DHG is Production.

## Required configuration

See `HelseId` and `PatientContext` in the API `appsettings.json`. Supply `ClientId`, `ClientAssertionJwk`, `DPoPJwk`, and synthetic Test aliases through an approved secret/configuration provider. Startup rejects empty facade scope, missing private keys, unsupported DHG environment names, and Test/Production mixing.

## Test and production status

The test alias endpoint is non-Production only and normally requires the same read policy as FHIR. In explicit test mode it is anonymous and binds the context to the configured fixed test subject.

Production is blocked until an approved patient-context authority and interoperability contract are implemented. That decision must cover authorization basis, issuer identity, subject/purpose binding, key storage/rotation, replay controls, audit, multi-instance Data Protection, and revocation/expiry.

An external smoke test must be explicitly opted in and use only an approved synthetic patient. The repository currently has no such credentialed smoke harness.

Swagger UI and the OpenAPI document are anonymous in non-Production environments. Clinical FHIR operations normally require a valid inbound HelseID DPoP access token and protected patient context. The explicit Development test mode removes only inbound authentication; outbound DHG calls still use HelseID client credentials, client assertion and DPoP, and therefore depend on an appropriately authorized DHG Test client registration.

When either the host or DHG environment is Production, Swagger and OpenAPI are disabled by default. Setting `Swagger:EnabledInProduction=true` exposes `/swagger`, `/swagger/v1/swagger.json`, and `/openapi/v1.json`, but all three require the normal authenticated HelseID `population.read` policy. Development test mode remains invalid against Production. Standard browser Swagger cannot perform the required HelseID DPoP flow by itself, so interactive production use requires an approved HelseID-aware backend/reverse proxy that keeps tokens and key material server-side.
