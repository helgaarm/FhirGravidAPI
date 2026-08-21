# HelseID setup

## Registrations

The facade needs:

- an inbound API registration for its configured audience and read scope;
- an actor client permitted to exchange the incoming subject token for `nhn:maternity-record` / `nhn:maternity-record/api`;
- one private asymmetric JWK for `private_key_jwt` client assertions;
- a separate private asymmetric JWK for DPoP.

For explicit Development/DHG Test only, the two private keys may be replaced by an approved HelseID TEST token-utility auth key plus the registered test client and organization claims. This exception is not available in authenticated or Production operation.

## Request flow

1. The client authenticates through HelseID and calls the facade with a DPoP-bound access token.
2. The auth gateway validates issuer, audience, lifetime, exact read scope, the DPoP proof, token/proof binding, and replay uniqueness.
3. The private facade independently validates the access token, verifies the gateway credential, and validates a short-lived patient context bound to the current HelseID `sub`.
4. The incoming access token is used only as `subject_token` in HelseID token exchange.
5. The exchanged DHG token and a destination-bound DPoP proof are sent to DHG; the inbound token is never forwarded to DHG.

For local Swagger testing, `DevelopmentTestMode:Enabled=true` replaces steps 1-4 with an anonymous inbound request. By default, the facade uses a server-side HelseID `client_credentials` request for the configured DHG resource and scope. When `HelseIdTestToken:Enabled=true`, it instead asks HelseID's TEST token utility for a fresh `accessTokenJwt` and matching `dPoPProof` bound to the exact DHG HTTP method and URL for every request. The facade still sends a DPoP-bound HelseID token to DHG. The mode requires host environment `Development`, `Dhg:Environment=Test`, loopback-only listeners, and a known loopback peer; do not expose that variant through a reverse proxy, tunnel, or port forwarding.

The repository's [Azure test deployment](azure-test-deployment.md) is the only supported remote exception. It explicitly sets `DevelopmentTestMode:AllowRemoteStaging=true` in `Staging`, keeps DHG in Test, and requires a trusted Container Apps ingress CIDR. It is still anonymous on the facade side and still requires server-side HelseID credentials for DHG. This exception is rejected when either host or DHG is Production.

## Required configuration

See `HelseId`, `HelseIdTestToken`, `AuthGateway`, and `PatientContext` in the API `appsettings.json`. Supply `ClientId`, `ClientAssertionJwk`, `DPoPJwk`, the gateway shared secret, and synthetic Test aliases through an approved secret/configuration provider. In the TEST-token exception, supply `HelseIdTestToken:AuthKey` and the approved synthetic organization claims instead of the two JWKs. Startup rejects empty facade scope, missing credentials, unsupported DHG environment names, Test/Production mixing, and any attempt to enable the utility outside explicit Development Test mode.

The relevant environment-variable form is:

```text
DevelopmentTestMode__Enabled=true
HelseIdTestToken__Enabled=true
HelseIdTestToken__AuthKey=<secret>
HelseIdTestToken__OrgnrParent=<nine-digit-test-organization-number>
HelseIdTestToken__ClientTenancyType=1
HelseId__ClientId=<registered-test-client-id>
```

Store only the stable auth key and claims as configuration. Never store, log, or reuse the returned `accessTokenJwt` or `dPoPProof`; the provider obtains a new request-bound pair for each DHG call. A plain `.env` file is not loaded automatically by .NET and must be imported into the process environment by the developer tooling if used.

For local Development, store the auth key outside the repository with:

```powershell
dotnet user-secrets set "HelseIdTestToken:AuthKey" "<secret>" --project src/PopulationDataFacade.Api
```

> **Mandatory TLS boundary:** `auth-gateway` listens with plaintext HTTP. It does not terminate TLS. A trusted HTTPS ingress or reverse proxy must terminate TLS before forwarding over a private loopback/pod-local connection to port 8080. Never publish port 8080 directly to an untrusted network. `AUTH_GATEWAY_EXTERNAL_SCHEME=https` only defines the canonical URL used for DPoP validation and forwarded metadata; it does not enable TLS.

For authenticated operation, expose only the trusted HTTPS ingress and configure both containers with the same random secret of at least 32 bytes. The TLS terminator must preserve the canonical `Host` value configured in `AUTH_GATEWAY_EXTERNAL_HOST`; the gateway rejects other Host values and rebuilds forwarding headers itself. The API listens on loopback port 8081 in the container deployment. A single-replica deployment may explicitly use `AUTH_GATEWAY_REPLAY_STORE=memory` with `AUTH_GATEWAY_SINGLE_REPLICA=true`; every multi-replica deployment must use `AUTH_GATEWAY_REPLAY_STORE=redis` with a TLS `AUTH_GATEWAY_REDIS_URL` so replay rejection is atomic across replicas.

```text
AUTH_GATEWAY_MODE=authenticate
AUTH_GATEWAY_UPSTREAM_URL=http://127.0.0.1:8081
AUTH_GATEWAY_EXTERNAL_SCHEME=https
AUTH_GATEWAY_EXTERNAL_HOST=<canonical-public-facade-host>
AUTH_GATEWAY_SHARED_SECRET=<random-32-byte-or-longer-secret>
AUTH_GATEWAY_REPLAY_STORE=redis
AUTH_GATEWAY_REDIS_URL=rediss://<credentials>@<redis-host>:6380/0
HELSEID_AUTHORITY=https://helseid-sts.nhn.no
HELSEID_AUDIENCE=nhn:population-data-facade
HELSEID_SCOPE=nhn:population-data-facade/read

AuthGateway__SharedSecret=<the-same-random-secret>
```

`AUTH_GATEWAY_EXTERNAL_SCHEME` and `AUTH_GATEWAY_EXTERNAL_HOST` are fixed configuration rather than caller-controlled forwarded headers because DPoP `htu` validation must use the canonical public request origin. The DPoP proof must target `https://<AUTH_GATEWAY_EXTERNAL_HOST>/<path-and-query>`. Requests carrying another `Host` value are rejected. The configured upstream must be an HTTP loopback origin; the gateway refuses non-loopback targets.

## Test and production status

The test alias endpoint is non-Production only and normally requires the same read policy as FHIR. In explicit test mode it is anonymous and binds the context to the configured fixed test subject.

For the exact alias → logical `patientId` → protected context → FHIR request sequence, including local user-secrets configuration and common errors, see [Patient ID and protected context for testing](patient-context-testing.md).

Production is blocked until an approved patient-context authority and interoperability contract are implemented. That decision must cover authorization basis, issuer identity, subject/purpose binding, key storage/rotation, replay controls, audit, multi-instance Data Protection, and revocation/expiry.

An external smoke test must be explicitly opted in and use only an approved synthetic patient. The repository currently has no such credentialed smoke harness.

Swagger UI and the OpenAPI document are anonymous in non-Production environments. Clinical FHIR operations normally require a valid inbound HelseID DPoP access token and protected patient context. The explicit Development test mode removes only inbound authentication. Outbound DHG calls still require a DPoP-bound HelseID authorization, using either the normal client-credentials/private-JWK flow or the separately enabled HelseID TEST-token utility. Both depend on an appropriately authorized DHG Test client registration.

When either the host or DHG environment is Production, Swagger and OpenAPI are disabled by default. Setting `Swagger:EnabledInProduction=true` exposes `/swagger`, `/swagger/v1/swagger.json`, and `/openapi/v1.json`, but all three require the normal authenticated HelseID `population.read` policy. Development test mode remains invalid against Production. Standard browser Swagger cannot perform the required HelseID DPoP flow by itself, so interactive production use requires an approved HelseID-aware backend/reverse proxy that keeps tokens and key material server-side.
