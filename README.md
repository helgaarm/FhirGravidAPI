# FHIR Population Data Facade for DHG

En skrivebeskyttet .NET 10-fasade som henter det aktive digitale helsekortet fra Norsk helsenetts DHG API og eksponerer et avgrenset FHIR R4-grensesnitt. DHG er eneste datakilde ved kjøring. Fasaden inneholder ingen syntetiske fallback-data, ingen spørreskjemakobling og ingen klinisk datacache.

## Løsningen

- `PopulationDataFacade.Api` validerer HelseID access-token og DPoP, håndhever scope, åpner en kortlivet kryptert pasientkontekst og returnerer FHIR JSON.
- `PopulationDataFacade.Core` inneholder kildeuavhengige populasjonsmodeller og FHIR-mapping.
- `PopulationDataFacade.Infrastructure` inneholder eksakte DHG-kontrakter, HelseID token exchange, DPoP-bevis, robust HTTP-klient og DHG-til-populasjon-mapping.
- `PopulationDataFacade.TestClient` er en server-side testklient med HelseID-innlogging. Den kaller bare fasaden, aldri DHG direkte.
- `tests` inneholder kontrakt-, mapping- og HTTP-integrasjonstester.

Støttede FHIR-operasjoner:

```text
GET /fhir/metadata
GET /fhir/Patient/{id}
GET /fhir/Observation?patient={id}[&code={system}|{code}]
GET /fhir/Encounter?patient={id}
```

Alle FHIR-svar har `application/fhir+json`. Søk uten treff returnerer en tom `searchset`-Bundle. Feil returneres som `OperationOutcome`. Fasaden tilbyr med hensikt ikke `$populate`.

## Forutsetninger

- .NET SDK 10.0.100 eller nyere kompatibel 10.0-SDK
- klient registrert i HelseID Test for token exchange til `nhn:maternity-record`
- API-registrering for fasadens audience og scope
- to private JWK-er: én til `private_key_jwt`, én til DPoP
- syntetisk testperson som finnes i DHG Test

De faktiske testendepunktene og DHG-kravene er dokumentert av NHN i [DHG miljøer](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/environmentsmd), [DHG autorisasjon](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/authorizationmd) og [HelseID token exchange](https://utviklerportal.nhn.no/informasjonstjenester/helseid/bruksmoenstre-og-eksempelkode/bruk-av-helseid/docs/teknisk-referanse/token_exchange_enmd).

## Konfigurasjon

Ikke legg private nøkler eller fødselsnummer i `appsettings.json`. Bruk secret store, miljøvariabler eller en administrert konfigurasjonsleverandør. Eksempel med miljøvariabler:

```powershell
$env:HelseId__ClientId = "<actor-client-id>"
$env:HelseId__ClientAssertionJwk = "<private-jwk-json>"
$env:HelseId__DPoPJwk = "<annen-private-jwk-json>"
$env:PatientContext__TestAliases__synthetic_1__LogicalId = "patient-test-1"
$env:PatientContext__TestAliases__synthetic_1__NationalIdentityNumber = "<syntetisk-fnr>"
```

Alias-konfigurasjon er kun tillatt utenfor Production. Endepunktet `POST /test/patient-context/{alias}` er deaktivert i Production. Konteksten bindes til det autentiserte HelseID-subjektet og kan ikke gjenbrukes av en annen innlogget bruker. En godkjent produksjonsmekanisme for utstedelse og tillit er ikke implementert; dette er en eksplisitt produksjonsblokker.

Testklienten trenger sin egen HelseID-klient og nøkler:

```powershell
$env:TestClient__ClientId = "<test-client-id>"
$env:TestClient__ClientAssertionJwk = "<private-jwk-json>"
$env:TestClient__DPoPJwk = "<annen-private-jwk-json>"
$env:TestClient__DefaultPatientAlias = "synthetic_1"
$env:TestClient__PatientContextHeaderName = "X-Patient-Context"
```

Konfigurasjonen valideres ved oppstart. `Dhg:Environment` må være `Test` eller `Production`; ukjente verdier og blandede Test/Production-endepunkter avvises. DHG audience/scope er låst til dokumenterte verdier, facade scope må være satt, og JWK-ene må inneholde asymmetrisk privat nøkkelmateriale.

## Bygg og kjør

```powershell
dotnet restore PopulationDataFacade.slnx
dotnet build PopulationDataFacade.slnx --no-restore
dotnet test PopulationDataFacade.slnx --no-build
dotnet run --project src/PopulationDataFacade.Api
dotnet run --project src/PopulationDataFacade.TestClient
```

API-et starter normalt på `https://localhost:7184`, og testklienten på `https://localhost:7284`. Swagger UI er tilgjengelig i ikke-produksjonsmiljøer. OpenAPI-dokumentet ligger på `/openapi/v1.json`.

## Drift

- Liveness: `/health/live`
- Readiness: `/health/ready` (foreløpig en grunn prosess-sjekk; ingen DHG/HelseID-avhengighet verifiseres)
- korrelasjons-ID: responsheader `X-Correlation-ID`
- OpenTelemetry: ASP.NET Core, utgående HTTP, DHG-målinger og token-exchange-spor
- OTLP-eksport aktiveres når `OTEL_EXPORTER_OTLP_ENDPOINT` er satt

Logger inneholder aldri access-token, privat nøkkel, fødselsnummer eller klinisk payload. DHG-kall bruker `nhn-patient-nin` kun som utgående header. Kun idempotente GET-kall retries ved timeout, 429 og relevante 5xx-feil; `Retry-After` respekteres.

Se [arkitektur](docs/architecture.md), [DHG→FHIR-ressursmapping](docs/dhg-fhir-resource-mapping.md), [mappingmatrise](docs/mapping.md), [DHG-kildeliste](docs/dhg-source-inventory.md), [populasjonsdekning](docs/dhg-population-coverage.md), [SDC-bruk](docs/sdc-usage.md), [HelseID-oppsett](docs/helseid-setup.md), [sikkerhetsarkitektur](docs/security-architecture.md), [drift](docs/operations.md) og [FHIR-eksempler](examples/fhir-queries.md) før produksjonssetting.

## Bevisste avgrensninger

- ingen demografikilde, fastlegekilde, Grunndata-adapter eller annen kilde enn DHG
- ingen Questionnaire/QuestionnaireResponse eller linkId-avhengighet
- ingen inferert provosert abort, diagnose, legemiddelnavn eller annen klinisk betydning fra fritekst
- ingen historisk rekonstruksjon utover eksplisitte DHG-felter
- ingen persistent caching av kliniske DHG-data
- `birthStatus` og kontakt-/demografifelter eksponeres ikke i første FHIR-flate; begrunnelsen står i mappingmatrisen
