# Drift og feilhåndtering

## Konfigurasjonsområder

| Område | Viktigste nøkler |
|---|---|
| `Dhg` | `Environment`, `BaseUrl`, `SourceSystem`, timeouts, connection lifetime, retryantall |
| `HelseId` | `Authority`, facade audience/scope, DHG audience/scope, client-ID og private JWK-er |
| `HelseIdTestToken` | eksplisitt DHG Test-only tokenverktøy, secret auth key og godkjente syntetiske klient-/organisasjonsclaims |
| `AuthGateway` | `SharedSecret`; samme tilfeldige verdi på minst 32 byte som `AUTH_GATEWAY_SHARED_SECRET` |
| `PatientContext` | headernavn, levetid og ikke-produksjonsaliaser |
| `DevelopmentTestMode` | eksplisitt anonym Swagger/DHG Test-modus, fast test-subjekt og Azure-malens avgrensede `AllowRemoteStaging` |
| `Swagger` | `EnabledInProduction`; standard `false`, og HelseID-policy håndheves når den er `true` i Production |
| `ReverseProxy` | `ForwardedHeadersEnabled`; bare på bak godkjent proxy/Container Apps slik at FHIR-baser bruker opprinnelig HTTPS-skjema |
| OpenTelemetry | standard `OTEL_*`-miljøvariabler |

Oppstart feiler ved manglende/ugyldig sikkerhetskonfigurasjon, ukjent `Dhg:Environment` eller blanding av Test og Production. Støttede DHG-miljøverdier er foreløpig bare `Test` og `Production`. `DevelopmentTestMode:Enabled=true` krever DHG Test. `HelseIdTestToken:Enabled=true` krever i tillegg aktiv Development-testmodus, en HTTPS-endpoint under `.test.nhn.no`, auth key, registrert client-ID og godkjente testclaims. I lokal Development kreves loopback-only listeners og kjent loopback-peer; ikke plasser denne varianten bak proxy, tunnel eller port-forwarding. Det eneste støttede fjernunntaket er Azure Test-malen med `Staging`, eksplisitt `AllowRemoteStaging=true` og obligatorisk Container Apps CIDR-begrensning. Begge varianter avvises mot Production. DHG Test-standard er `https://maternity-record.hit.test.nhn.no/api/maternity-record/v1/`; HelseID Test-standard er `https://helseid-sts.test.nhn.no`.

### Auth-gateway

Gatewayen konfigureres med miljøvariabler og feiler lukket ved ugyldig oppsett:

| Variabel | Betydning |
|---|---|
| `AUTH_GATEWAY_LISTEN_ADDR` | Intern HTTP-listener; standard `:8080` |
| `AUTH_GATEWAY_UPSTREAM_URL` | Privat API-origin; må være HTTP loopback uten path/query/credentials, standard `http://127.0.0.1:8081` |
| `AUTH_GATEWAY_MODE` | `authenticate`, eller `passthrough` bare i den avgrensede testtopologien |
| `AUTH_GATEWAY_EXTERNAL_SCHEME` | Kanonisk DPoP-skjema; må være `https` i autentisert modus og aktiverer ikke TLS |
| `AUTH_GATEWAY_EXTERNAL_HOST` | Kanonisk offentlig host, eventuelt med port; innkommende `Host` må samsvare |
| `AUTH_GATEWAY_SHARED_SECRET` | Tilfeldig intern credential på minst 32 byte; må samsvare med API-ets `AuthGateway__SharedSecret` |
| `AUTH_GATEWAY_REPLAY_STORE` | Eksplisitt `memory` eller `redis` |
| `AUTH_GATEWAY_SINGLE_REPLICA` | Må være `true` når replay-store er `memory` |
| `AUTH_GATEWAY_REDIS_URL` | Påkrevd for `redis`; eksterne tjenester må bruke `rediss://` |
| `HELSEID_AUTHORITY` | HelseID HTTPS-origin uten path, query eller credentials |
| `HELSEID_AUDIENCE` / `HELSEID_SCOPE` | Eksakt fasade-audience og påkrevd read-scope |

Gatewayen er en intern plaintext HTTP-tjeneste. Den må stå bak en betrodd TLS-terminator som videresender den kanoniske `Host`-verdien; port 8080 må ikke publiseres direkte til et ubeskyttet nett. API-port 8081 skal bare være tilgjengelig på loopback/pod-nettet. Alle caller-kontrollerte proxy-headere og interne gateway-credentials fjernes og bygges opp på nytt før videresending.

Bare `GET`, `HEAD` og `POST` tillates. Forespørselskroppen er begrenset til 1 MiB. Serverens read- og write-timeout er henholdsvis 15 og 60 sekunder; langsommere klienter eller responser avbrytes. Redis-feil i autentisert flerreplikadrift feiler lukket som autentiseringsfeil og må alarmeres.

Swagger/OpenAPI er av som standard når host- eller DHG-miljøet er Production. Sett bare `Swagger:EnabledInProduction=true` når produksjonstilgang er nødvendig; `/swagger`, `/swagger/v1/swagger.json` og `/openapi/v1.json` krever da et autentisert HelseID-subjekt med konfigurert fasadescope. Standard nettleser-Swagger støtter ikke denne DPoP-flyten alene; produksjons-UI forutsetter en godkjent HelseID-aware backend/reverse proxy.

## Health og telemetry

På auth-gatewayen bekrefter `/health/live` bare at gatewayprosessen svarer. Gatewayens `/health/ready` kaller det private API-ets `/health/ready`; nettverksfeil eller ikke-2xx blir `503`. API-ets readiness er foreløpig en grunn ASP.NET health-respons uten registrerte DHG/HelseID-sjekker. Readiness må derfor ikke tolkes som bevis på at HelseID eller DHG er tilgjengelig. Overvåk syntetiske, autoriserte ende-til-ende-kall i et separat kontrollert system dersom databehandleravtale og testmiljø tillater det.

Spor:

- ASP.NET Core request
- utgående HTTP, unntatt generisk DHG-span som filtreres for å unngå dynamisk record-ID i URL
- `PopulationDataFacade.HelseId` token exchange eller Development-only client credentials
- `PopulationDataFacade.HelseIdTestToken` HelseID TEST-tokenkall uten token, proof eller auth key i attributter
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
| metode utenfor `GET`/`HEAD`/`POST` | 405 | `not-supported` |
| gateway request body over 1 MiB | 413 | `too-costly` |
| gateway upstream/API utilgjengelig | 502 | `exception` |
| konfigurasjonsfeil | 500 | `exception` |
| HelseID/DHG utilgjengelig eller kontraktbrudd | 503 | `transient`/`processing` |

Detaljer fra HelseID/DHG returneres ikke til klienten. `OperationOutcome.diagnostics` inneholder en kontrollert tekst og ved uventet 500 en korrelasjons-ID.

## Oppgradering

Løsningen er bevisst beholdt på .NET 9. .NET 9 er en STS-utgivelse med støtte til 10. november 2026; produkteier må ha en godkjent oppgraderingsplan før denne datoen. Denne endringen migrerer ikke løsningen til .NET 10.

Før oppgradering av Firely, Duende, .NET eller DHG-kontrakten:

1. les leverandørens release notes og NHN-endringslogg
2. oppdater sentrale pakkeversjoner
3. oppdater kontraktfixture med dokumentert DHG-eksempel uten persondata
4. kjør alle kontrakt-, mapping- og integrasjonstester
5. verifiser CapabilityStatement og OpenAPI-dokument
6. test HelseID login, token exchange, DPoP nonce og begge DHG-kall i Test
