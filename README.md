# FHIR-fasade for DHG

En skrivebeskyttet .NET 9-fasade som henter det aktive digitale helsekortet fra Norsk helsenetts DHG API og eksponerer et avgrenset FHIR R4-grensesnitt. DHG er eneste datakilde ved kjøring. Fasaden inneholder ingen reservedata, spørreskjemakobling eller klinisk mellomlager.

## Løsningen

- `auth-gateway` validerer HelseID-tilgangstoken, DPoP-bevis, tilgangsomfang og gjenbruk av bevis før videresending til det private API-et.
- `PopulationDataFacade.Api` validerer JWT-et på nytt, krever gatewayens interne hemmelighet, åpner en kortlivet pasientkontekst og returnerer FHIR JSON.
- `PopulationDataFacade.Core` inneholder kildeuavhengige modeller og FHIR-mapping.
- `PopulationDataFacade.Infrastructure` inneholder DHG-kontrakter, HelseID-tokenutveksling, DPoP, HTTP-klient og DHG-mapping.
- `tests` inneholder kontraktstester, mappingtester og HTTP-integrasjonstester.

Støttede FHIR-operasjoner:

```text
GET /fhir/metadata
GET /fhir/Patient/{id}
GET /fhir/Observation?patient={id}[&code={system}|{code}][&category={code}][&date={prefix}{yyyy-MM-dd}]
GET /fhir/Encounter?patient={id}
GET /fhir/CareTeam?patient={id}
POST /fhir/Patient/_search                 identifier={nin}
POST /fhir/Observation/_search     patient.identifier={nin}[&code={system}|{code}][&category={code}][&date={prefix}{yyyy-MM-dd}]
POST /fhir/Encounter/_search       patient.identifier={nin}
POST /fhir/CareTeam/_search        patient.identifier={nin}
```

POST `_search` mottar fødselsnummeret i en `application/x-www-form-urlencoded`-kropp på maksimalt 4096 byte og bruker ikke `X-Patient-Context`. Utenfor lokal `DevelopmentTestMode` kreves et gyldig HelseID-token og DPoP-bevis med tilgangsomfanget `population.read`. Fasaden lager da en pseudonym `Patient.id` med HMAC. I lokal `DevelopmentTestMode` godtas bare fødselsnumre fra konfigurerte syntetiske aliaser.

GET-søk med fødselsnummer i URL støttes ikke. Se [pasient-ID og beskyttet testkontekst](docs/patient-context-testing.md) og [FHIR-eksempler](examples/fhir-queries.md).

Når DHG leverer en positiv `fetusesVitalSigns[].fosterId`, opprettes en minimal `Patient` for fosteret. Observasjoner om fosteret beholder mor som `subject` og refererer til fosteret med `focus`. Uten positiv `fosterId` beholdes funnet uten `focus` og uten konstruert fosteridentitet. Fosterressursen inneholder ikke fødselsnummer, navn, kjønn, fødselsdato eller identifikator. Manglende konsultasjonsdato gir `Encounter` uten `period` og observasjoner uten `effective[x]`. Se [DHG→FHIR-mapping](docs/dhg-fhir-resource-mapping.md).

Alle FHIR-svar bruker `application/fhir+json`. Søk uten treff returnerer en tom `searchset`-`Bundle`. Feil returneres som `OperationOutcome`. Fasaden implementerer ikke `$populate`.

FHIR-terminologien bruker NLK, SNOMED CT, Volven, LOINC og UCUM. Fasaden publiserer ikke egne kliniske koder. Sammensatte felt splittes ikke, fritekst tolkes ikke, og nullable boolske verdier gjøres ikke om til `false`. Manglende fosteridentitet eller konsultasjonsdato uttrykkes ved å utelate `focus`, fosterressurs, `period` og `effective[x]`. Se [mappingmatrisen](docs/mapping.md).

## Forutsetninger

- .NET SDK 9.0.317 eller nyere kompatibel 9.0-SDK
- Go 1.25 eller nyere for lokal bygging/testing av auth-gatewayen
- klient registrert i HelseID Test for token exchange til `nhn:maternity-record`
- API-registrering for fasadens målgruppe og tilgangsomfang
- to private JWK-er: én til `private_key_jwt` og én til DPoP. Den lokale TEST-tokenflyten bruker i stedet HelseID TEST-tokenverktøyet
- syntetisk testperson som finnes i DHG Test

De faktiske testendepunktene og DHG-kravene er dokumentert av NHN i [DHG miljøer](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/environmentsmd), [DHG autorisasjon](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/authorizationmd) og [HelseID token exchange](https://utviklerportal.nhn.no/informasjonstjenester/helseid/bruksmoenstre-og-eksempelkode/bruk-av-helseid/docs/teknisk-referanse/token_exchange_enmd).

## Konfigurasjon

Ikke legg private nøkler eller fødselsnummer i `appsettings.json`. Bruk miljøvariabler, user-secrets eller et godkjent hemmelighetslager.

```powershell
$env:HelseId__ClientId = "<actor-client-id>"
$env:HelseId__ClientAssertionJwk = "<private-jwk-json>"
$env:HelseId__DPoPJwk = "<annen-private-jwk-json>"
$env:AuthGateway__SharedSecret = "<tilfeldig-hemmelighet-minst-32-bytes>"
$env:PatientContext__PatientIdHmacKey = "<base64-kodet-tilfeldig-hemmelighet-minst-32-bytes>"
$env:PatientContext__TestAliases__synthetic_1__LogicalId = "patient-test-1"
$env:PatientContext__TestAliases__synthetic_1__NationalIdentityNumber = "<syntetisk-fnr>"
```

`PatientContext:PatientIdHmacKey` er påkrevd utenfor `DevelopmentTestMode`. Verdien må være en Base64-kodet hemmelighet på minst 32 byte og være separat fra gatewayhemmeligheten, Data Protection-nøklene og de private HelseID-nøklene. Endring av nøkkelen endrer de pseudonyme FHIR-ID-ene.

Testaliaser og `POST /test/patient-context/{alias}` er deaktivert i produksjon. Pasientkonteksten bindes til HelseID-subjektet; lokal `DevelopmentTestMode` bruker et fast testsubjekt. Repositoriet implementerer ingen produksjonsutsteder for `X-Patient-Context`. POST `_search` er en separat implementert flyt.

`LogicalId` er den lokale FHIR-ID-en, for eksempel `patient-test-1`. Den må følge formatet `[A-Za-z0-9.-]{1,64}`, være unik ved sammenligning som skiller mellom store og små bokstaver, og være forskjellig fra alle konfigurerte fødselsnumre. Aliasendepunktet returnerer verdien som `patientId` sammen med `patientContext`. Fødselsnummeret eksponeres ikke som FHIR-identifikator.

### Anonym Swagger mot DHG Test

`DevelopmentTestMode` godtas bare i `Development` med `Dhg:Environment=Test`, loopback-lytter og kjent loopback-motpart. Innkommende Swagger- og FHIR-kall er anonyme. Utgående DHG-kall bruker HelseID og DPoP. Når `HelseIdTestToken:Enabled=true`, hentes egne token og DPoP-bevis for `/status` og `/record`.

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

For å bruke HelseID TEST-tokenverktøyet i stedet for private JWK-er i denne modusen:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DevelopmentTestMode__Enabled = "true"
$env:HelseIdTestToken__Enabled = "true"
$env:HelseIdTestToken__AuthKey = "<secret-auth-key>"
$env:HelseIdTestToken__OrgnrParent = "<syntetisk-overordnet-test-orgnr-9-siffer>"
$env:HelseIdTestToken__OrgnrChild = "<syntetisk-behandlingssted-orgnr-9-siffer>"
$env:HelseIdTestToken__ClientTenancyType = "1"
$env:HelseIdTestToken__PractitionerNationalIdentityNumber = "<syntetisk-helsepersonell-fnr-11-siffer>"
$env:HelseIdTestToken__PractitionerHprNumber = "<syntetisk-hpr-nummer>"
$env:HelseIdTestToken__PractitionerName = "<syntetisk-helsepersonell-navn>"
$env:HelseIdTestToken__UserRoleCode = "LE"
$env:HelseIdTestToken__TreatmentFacilityName = "<navn-på-syntetisk-behandlingssted>"
$env:HelseId__ClientId = "<dhg-test-client-id>"
$env:PatientContext__TestAliases__synthetic_1__LogicalId = "patient-test-1"
$env:PatientContext__TestAliases__synthetic_1__NationalIdentityNumber = "<approved-synthetic-nin>"
dotnet run --project src/PopulationDataFacade.Api
```

I lokal utvikling lagres autentiseringsnøkkelen med .NET user-secrets:

```powershell
dotnet user-secrets set "HelseIdTestToken:AuthKey" "<secret-auth-key>" --project src/PopulationDataFacade.Api
```

Organisasjonsnummer, helsepersonellidentitet, HPR-nummer og rolle må beskrive én syntetisk identitet i NHNs testdata. `/status` bruker et maskintoken. `/record` bruker et brukertoken med HPR- og organisasjonskontekst. `accessTokenJwt` og `dPoPProof` lagres eller gjenbrukes ikke; fasaden henter et nytt par for hvert DHG-kall. .NET laster ikke `.env`-filer automatisk.

I Swagger:

1. Kall `POST /test/patient-context/{alias}` med `synthetic_1`.
2. Kopier `patientId` (den konfigurerte logiske FHIR-ID-en, ikke fødselsnummeret) og `patientContext` fra svaret.
3. Kall ønsket FHIR-operasjon, bruk samme `patientId` i ruten eller søket, og send `patientContext` i `X-Patient-Context`.

I lokal `DevelopmentTestMode` godtar de fire POST `_search`-operasjonene fødselsnummeret fra et konfigurert testalias. Fødselsnummeret sendes i forespørselskroppen. Returnerte FHIR-ressurser bruker aliasets `LogicalId` og inneholder ikke fødselsnummeret.

I autentisert drift krever POST-operasjonene HelseID-tilgangsomfanget `population.read`, `PatientContext:PatientIdHmacKey` og et innkommende subjekttoken. DHGs kontroller av samtykke, personstatus og aktivt helsekort kjøres før FHIR-mapping.

Modusen avvises ved oppstart utenfor lokal `Development` og mot annet enn DHG Test.

### Autentisert lokal gateway

Autentisert lokal kjøring består av det private API-et på loopback-port 8081 og `auth-gateway` på port 8080. Begge bruker samme hemmelighet på minst 32 byte. Gatewayen konfigureres med fasadens målgruppe, tilgangsomfang og offentlige vertsnavn. Den lokale enkeltprosessen bruker `AUTH_GATEWAY_REPLAY_STORE=memory` og `AUTH_GATEWAY_SINGLE_REPLICA=true`.

Gatewayen terminerer ikke TLS. DPoP-bevisets `htu` bruker HTTPS-adressen foran gatewayen, og `Host` må samsvare med `AUTH_GATEWAY_EXTERNAL_HOST`. Se [HelseID-oppsettet](docs/helseid-setup.md).

### Swagger i produksjon

Swagger og OpenAPI-dokumentene er deaktivert som standard i produksjon. Følgende innstilling aktiverer dem:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:Swagger__EnabledInProduction = "true"
```

Når innstillingen er aktiv, krever `/swagger`, `/swagger/v1/swagger.json` og `/openapi/v1.json` et HelseID-token og DPoP-bevis med `population.read`. Manglende identitet gir `401`; manglende tilgangsomfang gir `403`.

Swagger UI implementerer ikke HelseID- og DPoP-flyten.

Konfigurasjonen valideres ved oppstart. `Dhg:Environment` må være `Test` eller `Production`; blandede endepunkter avvises. Autentisert drift krever `PatientIdHmacKey`, klient-ID og private JWK-er. TEST-tokenverktøyet godtas bare når både `DevelopmentTestMode` og `HelseIdTestToken` er aktivert mot DHG Test.

## Bygg og kjør

```powershell
dotnet restore PopulationDataFacade.slnx --locked-mode
dotnet build PopulationDataFacade.slnx --no-restore
dotnet test PopulationDataFacade.slnx --no-build
Push-Location auth-gateway
go test ./...
Pop-Location
```

### Første kjøring og treg restore

Repositoriet bruker .NET 9. `dotnet run` gjenoppretter pakker og bygger prosjektet. Første kjøring tar derfor lengre tid når NuGet-pakkene ikke finnes lokalt.

Kjør restore eksplisitt én gang for å skille NuGet-steget fra kompileringen:

```powershell
dotnet --version
dotnet restore PopulationDataFacade.slnx --locked-mode --verbosity minimal
dotnet run --project src/PopulationDataFacade.Api --no-restore
```

`dotnet --version` skal vise en `9.0.x`-versjon. Når API-et er bygget og kildekoden er uendret, brukes:

```powershell
dotnet run --project src/PopulationDataFacade.Api --no-restore --no-build
```

Ikke bruk `--no-build` etter endringer i kode, prosjektfiler eller pakker. Hvis restore blir stående lenge på `Determining projects to restore...`, kjør kommandoen på nytt med `--verbosity normal` og kontroller tilgangen til den konfigurerte NuGet-kilden. `NU1900` eller `Unable to load the service index` betyr at NuGet ikke når pakke- eller sårbarhetstjenesten; kontroller nettverk, DNS og eventuell proxy. Tiden etter en fullført restore er build-tid, ikke restore-tid.

Startprofilen kjører API-et på `https://localhost:7184`. Dette er en direkte lokal adresse. Swagger UI og OpenAPI-dokumentet er anonyme utenfor produksjon på `/swagger` og `/openapi/v1.json`. Gjennom gatewayen i `authenticate`-modus krever også disse rutene HelseID og DPoP.

Lokal `Development` deaktiverer Windows Event Log og beholder konsoll- og feilsøkingslogging. Kontrollerte FHIR-feil returneres derfor som `OperationOutcome` selv om prosessen mangler tilgang til Windows Event Log.

## Drift

- Liveness: `/health/live`
- Readiness: `/health/ready` (prosessjekk uten kontroll av HelseID eller DHG)
- korrelasjons-ID: responsheader `X-Correlation-ID`
- OpenTelemetry: ASP.NET Core, utgående HTTP, DHG-målinger og token-exchange-spor
- OTLP-eksport aktiveres når `OTEL_EXPORTER_OTLP_ENDPOINT` er satt

Applikasjonen logger ikke tilgangstoken, private nøkler, fødselsnummer eller kliniske data. DHG-kall bruker `nhn-patient-nin` bare som utgående header. Idempotente GET-kall prøves på nytt ved tidsavbrudd, 429 og relevante 5xx-feil. `Retry-After` følges.

Se [pasient-ID og beskyttet testkontekst](docs/patient-context-testing.md), [arkitektur](docs/architecture.md), [DHG→FHIR-mapping](docs/dhg-fhir-resource-mapping.md), [attributtmapping](docs/dhg-facade-attribute-mapping.md), [mappingmatrise](docs/mapping.md), [DHG-kilde](docs/dhg-source-inventory.md), [datadekning](docs/dhg-population-coverage.md), [SDC-avgrensning](docs/sdc-usage.md), [HelseID-oppsett](docs/helseid-setup.md), [sikkerhet](docs/security.md), [drift](docs/operations.md) og [FHIR-eksempler](examples/fhir-queries.md).

## Bevisste avgrensninger

- ingen ekstern demografikilde, fastlegekilde, Grunndata-adapter eller annen kilde enn DHG; `CareTeam` bruker bare kontaktdata som kommer fra DHG
- ingen Questionnaire/QuestionnaireResponse eller linkId-avhengighet
- ingen inferert provosert abort, diagnose, legemiddelnavn eller annen klinisk betydning fra fritekst
- ingen historisk rekonstruksjon utover eksplisitte DHG-felter
- ingen persistent caching av kliniske DHG-data
- `birthInstitute` eksponeres som en inneholdt `Organization`-deltaker i `CareTeam`; `lastUpdatedBy` eksponeres ikke

## Lisens

Repositoryets originale kildekode er lisensiert under [MIT-lisensen](LICENSE). Tredjepartsavhengigheter beholder sine egne vilkår; se [tredjepartsavhengigheter og lisenser](THIRD-PARTY-NOTICES.md). CI avviser runtime-avhengigheter utenfor prosjektets gjennomgåtte MIT-, Apache-2.0-, BSD-2-Clause- og BSD-3-Clause-lisenser.
