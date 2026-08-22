# Security-arkitektur

## Trust boundaries

```mermaid
flowchart LR
    subgraph Caller [Caller-controlled zone]
        Client["FHIR client"]
    end

    subgraph Facade [Facade runtime]
        Gateway["auth-gateway"]
        Api["Private Facade API"]
        Infrastructure["Facade Infrastructure"]
    end

    subgraph External [Approved external services]
        HelseID["HelseID"]
        DHG["DHG API"]
        Telemetry["Telemetry backend"]
    end

    Client -->|"HelseID DPoP token<br/>+ subject-bound context"| Gateway
    Gateway -->|"Validated request<br/>+ private shared credential"| Api
    Gateway -->|"Discovery / JWKS"| HelseID
    Api --> Infrastructure
    Infrastructure -->|"Subject token exchange<br/>+ private_key_jwt / DPoP"| HelseID
    Infrastructure -->|"Exchanged DPoP token<br/>+ NIN header"| DHG
    Api -->|"Redacted low-cardinality signals"| Telemetry
```

Development test mode endrer bare de to første pilene: Swagger/FHIR-caller er anonym, og fasaden henter DPoP-bound DHG authorization server-side. Normalt brukes `client_credentials`; en ekstra HelseID TEST-token provider som er disabled by default, kan i stedet opprette et nytt token/proof-par for hver eksakte DHG request, i samsvar med smartOppgaves test flow. Modusen krever lokal Development, loopback-only listeners og en kjent loopback peer; eksponering via proxy, tunnel eller port forwarding er forbudt. Den krever DHG Test og avvises i alle andre environments og mot Production.

FHIR layer mottar aldri DHG JSON paths eller en alternativ data source. En godkjent syntetisk NIN lagres i secret configuration og kan i lokal Development test mode mottas transient i POST form body. Derfra inngår den i request context eller en beskyttet patient context og sendes i påkrevd outbound DHG header. Den er aldri en FHIR logical ID, URL parameter, response field, log field eller telemetry tag. De lokale POST-rutene registreres ikke utenfor lokal Development.

## Implementerte controls

- inbound HelseID access-token- og DPoP-validation i Go auth gateway med `golang-jwt`, `keyfunc` og NHNs anbefalte `AxisCommunications/go-dpop` library
- eksakte kontroller av `at+jwt` type, issuer, single audience, expiry, not-before, scope, proof signature, `htm`/`htu`, ti sekunders freshness, `ath`, `cnf.jkt`, asymmetric public JWK og unik `jti`
- uavhengig JWT validation i privat .NET API, som også krever en delt gateway credential kontrollert i constant time
- gateway-stripping av caller-supplied internal credentials og deployment ingress rettet bare mot gateway-porten
- FHIR `OperationOutcome` for authorization- og application failures
- subject-bound, time-limited Data Protection patient context
- gates for consent, deceased, active record, record ID og `ACTIVE` status før mapping
- HTTPS-only configuration og lukket Test/Production environment validation
- separate konfigurerte roller for client assertion key og DPoP key
- ingen persistent clinical cache eller runtime fallback data
- no-store FHIR responses
- normaliserte DHG activity tags og undertrykking av generiske DHG URL spans
- kontrollerte correlation IDs og ingen raw upstream error body i client responses
- Production Swagger/OpenAPI er disabled by default når host- eller DHG environment er Production, og beskyttes av normal HelseID read policy når det aktiveres eksplisitt; interactive UI krever en godkjent HelseID-aware backend/proxy fordi standard Swagger UI ikke implementerer nødvendig DPoP-håndtering

## Production gates

- implementer og godkjenn production patient-context authority
- konfigurer en delt kryptert Data Protection key ring for mer enn én instance
- konfigurer Redis atomic replay store før mer enn én instance kjøres; memory store nekter startup med mindre single-replica operation er eksplisitt deklarert
- generer og roter en tilfeldig delt gateway credential på minst 32 bytes, og hold API-porten privat i sidecar network
- godkjenn eksakte HelseID/DHG host allowlists og deployment egress policy
- konfigurer trusted proxies, canonical public FHIR URL og allowed hosts
- legg til meningsfull readiness semantics og kontrollert ekstern synthetic monitoring
- fullfør reell HelseID Test/DHG Test interoperability, penetration-, privacy- og clinical terminology review
- etabler locked restore, CI security gates, immutable image policy og rollback evidence

Se [security.md](security.md) for operative detaljer og [helseid-setup.md](helseid-setup.md) for identity configuration.
