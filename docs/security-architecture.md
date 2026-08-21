# Security architecture

## Trust boundaries

```text
FHIR client -- HelseID DPoP token + subject-bound context --> Auth gateway
Auth gateway -- validated request + private shared credential --> Facade API
Auth gateway -- discovery/JWKS --> HelseID
Facade API -- subject token exchange + private_key_jwt/DPoP --> HelseID
Facade Infrastructure -- exchanged DPoP token + NIN header --> DHG
Facade -- redacted low-cardinality signals --> telemetry backend
```

Development test mode changes only the first two arrows: the Swagger/FHIR caller is anonymous, and the facade obtains a DPoP-bound DHG authorization server-side. It normally uses `client_credentials`; an additional disabled-by-default HelseID TEST-token provider can instead mint a fresh token/proof pair for each exact DHG request, matching smartOppgave's test flow. The normal Development variant requires loopback-only listeners and a known loopback peer; proxy/tunnel/port-forward exposure is prohibited. The repository's Azure test template is an explicit Staging exception that requires `AllowRemoteStaging=true` and an ingress CIDR enforced by Container Apps. Both variants require DHG Test and are rejected against Production.

The FHIR layer never receives DHG JSON paths or an alternative data source. NIN exists only inside the protected context and the required outbound DHG header; it is never a FHIR logical ID, URL parameter, log field, or telemetry tag.

## Controls implemented

- inbound HelseID access-token and DPoP validation in the Go auth gateway using `golang-jwt`, `keyfunc`, and NHN's recommended `AxisCommunications/go-dpop` library;
- exact `at+jwt` type, issuer, single audience, expiry, not-before, scope, proof signature, `htm`/`htu`, ten-second freshness, `ath`, `cnf.jkt`, asymmetric public JWK, and unique `jti` checks;
- independent JWT validation in the private .NET API, which also requires a constant-time-checked shared gateway credential;
- gateway stripping of caller-supplied internal credentials and deployment ingress targeting only the gateway port;
- FHIR `OperationOutcome` for authorization and application failures;
- subject-bound, time-limited Data Protection patient context;
- consent, deceased, active-record, record-ID and `ACTIVE` status gates before mapping;
- HTTPS-only configuration and closed Test/Production environment validation;
- separate configured client-assertion and DPoP key roles;
- no persistent clinical cache or runtime fallback data;
- no-store FHIR responses;
- normalized DHG activity tags and suppression of generic DHG URL spans;
- controlled correlation IDs and no raw upstream error body in client responses.
- production Swagger/OpenAPI disabled by default whenever the host or DHG environment is Production and protected by the normal HelseID read policy when explicitly enabled; interactive UI use requires an approved HelseID-aware backend/proxy because standard Swagger UI does not implement the required DPoP handling.

## Production gates

- implement and approve the production patient-context authority;
- configure a shared encrypted Data Protection key ring for more than one instance;
- configure the Redis atomic replay store before running more than one instance; the memory store refuses to start unless single-replica operation is explicitly declared;
- generate and rotate a random gateway shared credential of at least 32 bytes, and keep the API port private to the sidecar network;
- approve exact HelseID/DHG host allowlists and deployment egress policy;
- configure trusted proxies/canonical public FHIR URL and allowed hosts;
- add meaningful readiness semantics and controlled external synthetic monitoring;
- complete real HelseID Test/DHG Test interoperability, penetration, privacy and clinical terminology review;
- establish locked restore, CI security gates, immutable image policy and rollback evidence.

See [security.md](security.md) for operational details and [helseid-setup.md](helseid-setup.md) for identity configuration.
