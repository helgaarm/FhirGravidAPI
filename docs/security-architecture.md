# Security architecture

## Trust boundaries

```text
FHIR client -- HelseID DPoP token + subject-bound context --> Facade API
Facade API -- subject token exchange + private_key_jwt/DPoP --> HelseID
Facade Infrastructure -- exchanged DPoP token + NIN header --> DHG
Facade -- redacted low-cardinality signals --> telemetry backend
```

The FHIR layer never receives DHG JSON paths or an alternative data source. NIN exists only inside the protected context and the required outbound DHG header; it is never a FHIR logical ID, URL parameter, log field, or telemetry tag.

## Controls implemented

- issuer, audience, lifetime, DPoP and exact-scope validation;
- FHIR `OperationOutcome` for authorization and application failures;
- subject-bound, time-limited Data Protection patient context;
- consent, deceased, active-record, record-ID and `ACTIVE` status gates before mapping;
- HTTPS-only configuration and closed Test/Production environment validation;
- separate configured client-assertion and DPoP key roles;
- no persistent clinical cache or runtime fallback data;
- no-store FHIR and test-client responses;
- normalized DHG activity tags and suppression of generic DHG URL spans;
- controlled correlation IDs and no raw upstream error body in client responses.

## Production gates

- implement and approve the production patient-context authority;
- configure a shared encrypted Data Protection key ring for more than one instance;
- approve exact HelseID/DHG host allowlists and deployment egress policy;
- configure trusted proxies/canonical public FHIR URL and allowed hosts;
- add meaningful readiness semantics and controlled external synthetic monitoring;
- complete real HelseID Test/DHG Test interoperability, penetration, privacy and clinical terminology review;
- establish locked restore, CI security gates, immutable image policy and rollback evidence.

See [security.md](security.md) for operational details and [helseid-setup.md](helseid-setup.md) for identity configuration.
