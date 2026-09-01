# Drift og feilhåndtering

## API-konfigurasjon

| Område | Nøkler |
|---|---|
| `Dhg` | `Environment`, `BaseUrl`, `SourceSystem`, tidsgrenser og antall nye forsøk |
| `HelseId` | `Authority`, målgrupper, tilgangsomfang, klient-ID og private JWK-er |
| `HelseIdTestToken` | Autentiseringsnøkkel og syntetisk HPR-, rolle- og organisasjonskontekst for DHG Test |
| `AuthGateway` | `SharedSecret`, minst 32 byte og lik `AUTH_GATEWAY_SHARED_SECRET` |
| `PatientContext` | Headernavn, levetid, `PatientIdHmacKey` og testaliaser |
| `DevelopmentTestMode` | Lokal anonym testmodus og fast testsubjekt |
| `Swagger` | `EnabledInProduction`; standardverdien er `false` |
| `ReverseProxy` | `ForwardedHeadersEnabled` |
| OpenTelemetry | Standardvariabler med prefikset `OTEL_` |

Oppstart avvises ved manglende sikkerhetskonfigurasjon, ukjent `Dhg:Environment` eller blanding av test- og produksjonsendepunkter. Gyldige DHG-miljøer er `Test` og `Production`.

Utenfor `DevelopmentTestMode` kreves en Base64-kodet `PatientContext:PatientIdHmacKey` på minst 32 byte. `DevelopmentTestMode:Enabled=true` krever `Development`, DHG Test, en loopback-lytter og en kjent loopback-motpart. `HelseIdTestToken:Enabled=true` krever i tillegg en `.test.nhn.no`-URL, autentiseringsnøkkel, klient-ID og en fullstendig syntetisk testidentitet.

## Auth-gateway

Gatewayen konfigureres med miljøvariabler:

| Variabel | Implementert betydning |
|---|---|
| `AUTH_GATEWAY_LISTEN_ADDR` | HTTP-lytter; standard `:8080` |
| `AUTH_GATEWAY_UPSTREAM_URL` | HTTP-loopback uten sti, spørring eller legitimasjon; standard `http://127.0.0.1:8081` |
| `AUTH_GATEWAY_MODE` | `authenticate` validerer HelseID og DPoP. `passthrough` videresender uten autentisering og har ingen miljøsperre |
| `AUTH_GATEWAY_EXTERNAL_SCHEME` | Må være `https` i autentisert modus; aktiverer ikke TLS |
| `AUTH_GATEWAY_EXTERNAL_HOST` | Offentlig vertsnavn som må samsvare med innkommende `Host` |
| `AUTH_GATEWAY_SHARED_SECRET` | Intern hemmelighet på minst 32 byte |
| `AUTH_GATEWAY_REPLAY_STORE` | `memory` i det dokumenterte enkeltprosessoppsettet |
| `AUTH_GATEWAY_SINGLE_REPLICA` | `true` i det dokumenterte enkeltprosessoppsettet |
| `HELSEID_AUTHORITY` | HelseID-URL med HTTPS uten sti, spørring eller legitimasjon |
| `HELSEID_AUDIENCE` / `HELSEID_SCOPE` | Fasadens målgruppe og påkrevde lesetilgang |

Gatewayen terminerer ikke TLS. Den konfigurerte oppstrømsadressen må være loopback. Klientstyrte proxyheadere og den interne gatewayheaderen fjernes før videresending.

Bare `GET`, `HEAD` og `POST` godtas. Forespørselskroppen er begrenset til 1 MiB. Lese- og skrivetidsgrensene er henholdsvis 15 og 60 sekunder.

## Endepunkttilgang

| Tilgangsvei | Metadata | FHIR-data | Swagger/OpenAPI |
|---|---|---|---|
| Gateway i `authenticate`-modus | HelseID og DPoP | HelseID og DPoP | HelseID og DPoP |
| Direkte API i lokal `DevelopmentTestMode` | Anonym | Anonym | Anonym |
| Direkte API uten testmodus | Anonym | Krever gatewayvalidert HelseID-token | Anonymt utenfor produksjon; deaktivert som standard i produksjon |

Gatewayens `/health/live` og `/health/ready` er anonyme. `/health/live` kontrollerer bare gatewayprosessen. `/health/ready` videresender API-ets readiness-sjekk. API-ets readiness-sjekk kontrollerer prosessen, men ikke HelseID eller DHG.

## Telemetri

Egne spor omfatter HelseID-tokenutveksling, HelseID TEST-tokenkall og DHG-kall. DHG-spor bruker den normaliserte verdien `dhg.operation=status|record`. Målingene er `dhg.request.duration` og `dhg.request.errors`.

Token, DPoP-bevis, autentiseringsnøkkel og DHG-svar legges ikke i egendefinerte spor. Generisk HTTP-sporing mot DHG er filtrert bort for å unngå dynamisk post-ID i URL-attributter.

## Feiloversettelse

| Hendelse | HTTP | FHIR `issue.code` |
|---|---:|---|
| Ugyldig eller manglende pasientkontekst | 400 | `invalid` |
| Ugyldig POST-skjema eller fødselsnummerformat | 400 | `invalid` |
| Manglende eller ugyldig token | 401 | `security` |
| Manglende samtykke eller forbudt tilgang | 403 | `forbidden` |
| Ukjent pasient eller manglende aktivt helsekort | 404 | `not-found` |
| DHG-begrensning | 429 | `throttled` |
| Metode utenfor `GET`, `HEAD` og `POST` | 405 | `not-supported` |
| Forespørselskropp over 1 MiB | 413 | `too-costly` |
| Gatewayen får ikke kontakt med API-et | 502 | `exception` |
| Konfigurasjonsfeil | 500 | `exception` |
| HelseID- eller DHG-feil | 503 | `transient` eller `processing` |

Rå feildetaljer fra HelseID og DHG returneres ikke. En uventet 500-feil inneholder en korrelasjons-ID.
