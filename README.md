# FHIR Population Data Facade for DHG

En skrivebeskyttet .NET 10-fasade som henter det aktive digitale helsekortet fra Norsk helsenetts DHG API og eksponerer et avgrenset FHIR R4-grensesnitt. DHG er eneste datakilde ved kjøring. Fasaden inneholder ingen syntetiske fallback-data, ingen spørreskjemakobling og ingen klinisk datacache.

## Løsningen

- `PopulationDataFacade.Api` validerer normalt HelseID access-token og DPoP, håndhever scope, åpner en kortlivet kryptert pasientkontekst og returnerer FHIR JSON. En eksplisitt testmodus kan gjøre Swagger/FHIR anonymt lokalt, eller i repositoryets IP-begrensede Azure Staging-mal, mens fasaden bruker server-side HelseID client credentials mot DHG Test.
- `PopulationDataFacade.Core` inneholder kildeuavhengige populasjonsmodeller og FHIR-mapping.
- `PopulationDataFacade.Infrastructure` inneholder eksakte DHG-kontrakter, HelseID token exchange, DPoP-bevis, robust HTTP-klient og DHG-til-populasjon-mapping.
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

Alias-konfigurasjon er kun tillatt utenfor Production. Endepunktet `POST /test/patient-context/{alias}` er deaktivert i Production. Normalt bindes konteksten til det autentiserte HelseID-subjektet og kan ikke gjenbrukes av en annen innlogget bruker; Development-testmodus binder den i stedet til et fast konfigurert test-subjekt. En godkjent produksjonsmekanisme for utstedelse og tillit er ikke implementert; dette er en eksplisitt produksjonsblokker.

### Anonym Swagger mot DHG Test

Som standard er denne modusen bare for lokal testing. Den kan starte i `Development` med `Dhg:Environment=Test`; eksplisitte wildcard-/ikke-loopback-listenere avvises, og forespørsler uten en kjent loopback-peer avvises. Modusen må da ikke publiseres gjennom reverse proxy, tunnel eller port-forwarding. Innkommende Swagger/FHIR-kall er anonyme, mens fasaden bruker `client_credentials`, client assertion og DPoP server-side for DHG-kall. HelseID-klienten må være godkjent for de DHG Test-operasjonene som skal prøves.

Det finnes ett eksplisitt unntak for repositoryets Azure Test-mal: `Staging` kan bruke `DevelopmentTestMode:AllowRemoteStaging=true` mot DHG Test når Container Apps samtidig begrenser ingress til en obligatorisk, kontrollert CIDR. Dette er ikke en generell applikasjonsinnstilling og skal bare settes av [Azure testdeploymentet](docs/azure-test-deployment.md). Testmodus avvises fortsatt når host eller DHG er Production.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DevelopmentTestMode__Enabled = "true"
$env:HelseId__ClientId = "<dhg-test-client-id>"
$env:HelseId__ClientAssertionJwk = "<private-jwk-json>"
$env:HelseId__DPoPJwk = "<separate-private-jwk-json>"
$env:PatientContext__TestAliases__synthetic_1__LogicalId = "patient-test-1"
$env:PatientContext__TestAliases__synthetic_1__NationalIdentityNumber = "<approved-synthetic-nin>"
dotnet run --project src/PopulationDataFacade.Api
```

I Swagger:

1. Kall `POST /test/patient-context/{alias}` med `synthetic_1`.
2. Kopier `patientContext` og `patientId` fra svaret.
3. Kall ønsket FHIR-operasjon, bruk `patientId`, og lim `patientContext` inn i `X-Patient-Context`-feltet Swagger viser.

Modusen avvises ved oppstart utenfor lokal Development eller den eksplisitt tillatte, IP-begrensede Azure Staging-malen, og alltid mot annet enn DHG Test. Den må aldri aktiveres i QA eller Production.

### Swagger i Production

Swagger og begge OpenAPI-dokumentene er deaktivert som standard i Production. Dersom de er nødvendige i et kontrollert produksjonsmiljø, må de aktiveres eksplisitt:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:Swagger__EnabledInProduction = "true"
```

Når dette er aktivert, krever `/swagger`, `/swagger/v1/swagger.json` og `/openapi/v1.json` et gyldig HelseID DPoP access-token som oppfyller den samme `population.read`-policyen som FHIR-operasjonene. En manglende identitet får `401`, og en autentisert identitet uten konfigurert fasadescope får `403`. Den samme produksjonsgrensen brukes dersom enten host-miljøet eller `Dhg:Environment` er `Production`; `DevelopmentTestMode` kan aldri brukes mot DHG Production.

En vanlig Swagger UI i nettleseren er ikke i seg selv en HelseID/DPoP-klient. Produksjons-UI må derfor ligge bak en godkjent HelseID-aware backend/reverse proxy som håndterer innlogging og DPoP server-side for alle UI- og API-kall. Uten en slik komponent bør bare OpenAPI-dokumentet hentes med et DPoP-kompatibelt verktøy; access-token skal ikke limes inn i nettleseren.

Konfigurasjonen valideres ved oppstart. `Dhg:Environment` må være `Test` eller `Production`; ukjente verdier og blandede Test/Production-endepunkter avvises. DHG audience/scope er låst til dokumenterte verdier, facade scope må være satt, og JWK-ene må inneholde asymmetrisk privat nøkkelmateriale.

## Bygg og kjør

```powershell
dotnet restore PopulationDataFacade.slnx
dotnet build PopulationDataFacade.slnx --no-restore
dotnet test PopulationDataFacade.slnx --no-build
dotnet run --project src/PopulationDataFacade.Api
```

API-et starter normalt på `https://localhost:7184`. Swagger UI og OpenAPI-dokumentet er tilgjengelig uten innlogging i ikke-produksjonsmiljøer på henholdsvis `/swagger` og `/openapi/v1.json`. I Production er de deaktivert som standard og HelseID-beskyttet når de aktiveres eksplisitt. Kliniske FHIR-operasjoner krever normalt HelseID/DPoP; når den eksplisitte Development-testmodusen er aktiv, er de anonyme og fasaden autentiserer i stedet server-side mot DHG Test.

## Drift

- Liveness: `/health/live`
- Readiness: `/health/ready` (foreløpig en grunn prosess-sjekk; ingen DHG/HelseID-avhengighet verifiseres)
- korrelasjons-ID: responsheader `X-Correlation-ID`
- OpenTelemetry: ASP.NET Core, utgående HTTP, DHG-målinger og token-exchange-spor
- OTLP-eksport aktiveres når `OTEL_EXPORTER_OTLP_ENDPOINT` er satt

Logger inneholder aldri access-token, privat nøkkel, fødselsnummer eller klinisk payload. DHG-kall bruker `nhn-patient-nin` kun som utgående header. Kun idempotente GET-kall retries ved timeout, 429 og relevante 5xx-feil; `Retry-After` respekteres.

Se [Azure testdeployment](docs/azure-test-deployment.md), [arkitektur](docs/architecture.md), [DHG→FHIR-ressursmapping](docs/dhg-fhir-resource-mapping.md), [mappingmatrise](docs/mapping.md), [DHG-kildeliste](docs/dhg-source-inventory.md), [populasjonsdekning](docs/dhg-population-coverage.md), [SDC-bruk](docs/sdc-usage.md), [HelseID-oppsett](docs/helseid-setup.md), [sikkerhetsarkitektur](docs/security-architecture.md), [drift](docs/operations.md) og [FHIR-eksempler](examples/fhir-queries.md) før produksjonssetting.

## Bevisste avgrensninger

- ingen demografikilde, fastlegekilde, Grunndata-adapter eller annen kilde enn DHG
- ingen Questionnaire/QuestionnaireResponse eller linkId-avhengighet
- ingen inferert provosert abort, diagnose, legemiddelnavn eller annen klinisk betydning fra fritekst
- ingen historisk rekonstruksjon utover eksplisitte DHG-felter
- ingen persistent caching av kliniske DHG-data
- `birthStatus` og kontakt-/demografifelter eksponeres ikke i første FHIR-flate; begrunnelsen står i mappingmatrisen
