# Drift og feilhåndtering

## Konfigurasjonsområder

| Område | Viktigste nøkler |
|---|---|
| `Dhg` | `Environment`, `BaseUrl`, `SourceSystem`, timeouts, connection lifetime, retryantall |
| `HelseId` | `Authority`, facade audience/scope, DHG audience/scope, client-ID og private JWK-er |
| `PatientContext` | headernavn, levetid og ikke-produksjonsaliaser |
| `DevelopmentTestMode` | eksplisitt anonym Swagger/DHG Test-modus, fast test-subjekt og Azure-malens avgrensede `AllowRemoteStaging` |
| `Swagger` | `EnabledInProduction`; standard `false`, og HelseID-policy håndheves når den er `true` i Production |
| `ReverseProxy` | `ForwardedHeadersEnabled`; bare på bak godkjent proxy/Container Apps slik at FHIR-baser bruker opprinnelig HTTPS-skjema |
| OpenTelemetry | standard `OTEL_*`-miljøvariabler |

Oppstart feiler ved manglende/ugyldig sikkerhetskonfigurasjon, ukjent `Dhg:Environment` eller blanding av Test og Production. Støttede DHG-miljøverdier er foreløpig bare `Test` og `Production`. `DevelopmentTestMode:Enabled=true` krever DHG Test. I lokal Development kreves loopback-only listeners og kjent loopback-peer; ikke plasser denne varianten bak proxy, tunnel eller port-forwarding. Det eneste støttede fjernunntaket er Azure Test-malen med `Staging`, eksplisitt `AllowRemoteStaging=true` og obligatorisk Container Apps CIDR-begrensning. Begge varianter avvises mot Production. DHG Test-standard er `https://maternity-record.hit.test.nhn.no/api/maternity-record/v1/`; HelseID Test-standard er `https://helseid-sts.test.nhn.no`.

Swagger/OpenAPI er av som standard når host- eller DHG-miljøet er Production. Sett bare `Swagger:EnabledInProduction=true` når produksjonstilgang er nødvendig; `/swagger`, `/swagger/v1/swagger.json` og `/openapi/v1.json` krever da et autentisert HelseID-subjekt med konfigurert fasadescope. Standard nettleser-Swagger støtter ikke denne DPoP-flyten alene; produksjons-UI forutsetter en godkjent HelseID-aware backend/reverse proxy.

## Health og telemetry

`/health/live` bekrefter at prosessen svarer. `/health/ready` er foreløpig en grunn ASP.NET health-respons uten registrerte DHG/HelseID-sjekker. Den må ikke tolkes som bevis på ekstern integrasjon eller produksjonsklarhet. Overvåk syntetiske, autoriserte ende-til-ende-kall i et separat kontrollert system dersom databehandleravtale og testmiljø tillater det.

Spor:

- ASP.NET Core request
- utgående HTTP, unntatt generisk DHG-span som filtreres for å unngå dynamisk record-ID i URL
- `PopulationDataFacade.HelseId` token exchange eller Development-only client credentials
- `PopulationDataFacade.Dhg` med normalisert `dhg.operation=status|record`

Målinger:

- `dhg.request.duration`
- `dhg.request.errors` med lavkardinalitetsårsak

Ingen patient-ID, NIN, token, kodeverdi eller klinisk data brukes som telemetry-attributt.

## Feiloversettelse

| Hendelse | HTTP | FHIR issue |
|---|---:|---|
| ugyldig/manglende pasientkontekst | 400 | `invalid` |
| manglende/ugyldig token | 401 | `security` |
| manglende samtykke/forbudt | 403 | `forbidden` |
| ukjent pasient/ingen aktiv record | 404 | `not-found` |
| DHG rate limit | 429 | `throttled` |
| konfigurasjonsfeil | 500 | `exception` |
| HelseID/DHG utilgjengelig eller kontraktbrudd | 503 | `transient`/`processing` |

Detaljer fra HelseID/DHG returneres ikke til klienten. `OperationOutcome.diagnostics` inneholder en kontrollert tekst og ved uventet 500 en korrelasjons-ID.

## Oppgradering

Før oppgradering av Firely, Duende, .NET eller DHG-kontrakten:

1. les leverandørens release notes og NHN-endringslogg
2. oppdater sentrale pakkeversjoner
3. oppdater kontraktfixture med dokumentert DHG-eksempel uten persondata
4. kjør alle kontrakt-, mapping- og integrasjonstester
5. verifiser CapabilityStatement og OpenAPI-dokument
6. test HelseID login, token exchange, DPoP nonce og begge DHG-kall i Test
