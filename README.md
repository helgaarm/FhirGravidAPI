# FHIR Population Data Facade for DHG

En skrivebeskyttet .NET 9-fasade som henter det aktive digitale helsekortet fra Norsk helsenetts DHG API og eksponerer et avgrenset FHIR R4-grensesnitt. DHG er eneste datakilde ved kjøring. Fasaden inneholder ingen syntetiske fallback-data, ingen spørreskjemakobling og ingen klinisk datacache.

## Løsningen

- `auth-gateway` er den eksterne inngangen og validerer HelseID access-token, DPoP, scope og replay med åpne Go-biblioteker før den proxier til det private API-et.
- `PopulationDataFacade.Api` krever gatewayens interne credential, validerer JWT-et på nytt, åpner en kortlivet kryptert pasientkontekst og returnerer FHIR JSON. En eksplisitt lokal testmodus kan gjøre Swagger/FHIR anonymt mens fasaden skaffer HelseID-autorisasjon server-side mot DHG Test.
- `PopulationDataFacade.Core` inneholder kildeuavhengige populasjonsmodeller og FHIR-mapping.
- `PopulationDataFacade.Infrastructure` inneholder eksakte DHG-kontrakter, HelseID token exchange, DPoP-bevis, den avgrensede HelseID TEST-tokenflyten, robust HTTP-klient og DHG-til-populasjon-mapping.
- `tests` inneholder kontrakt-, mapping- og HTTP-integrasjonstester.

Støttede FHIR-operasjoner:

```text
GET /fhir/metadata
GET /fhir/Patient/{id}
GET /fhir/Observation?patient={id}[&code={system}|{code}]
GET /fhir/Encounter?patient={id}
GET /fhir/CareTeam?patient={id}
POST /fhir/Patient/_search                 identifier={nin}
POST /fhir/Observation/_search     patient.identifier={nin}[&code={system}|{code}][&category={code}][&date={prefix}{yyyy-MM-dd}]
POST /fhir/Encounter/_search       patient.identifier={nin}
POST /fhir/CareTeam/_search        patient.identifier={nin}
```

POST `_search` tar fødselsnummeret i en `application/x-www-form-urlencoded` request body på maksimalt 4096 bytes og krever ikke `X-Patient-Context`. Utenfor lokal `DevelopmentTestMode`, inkludert Production, krever operasjonene et gyldig HelseID DPoP access-token med fasadens `population.read`-policy. Fasaden lager da en stabil, pseudonym FHIR `Patient.id` med HMAC; fødselsnummeret returneres aldri. I lokal `DevelopmentTestMode` er bare fødselsnummer som finnes i et konfigurert syntetisk alias tillatt, og aliasets `LogicalId` brukes.

GET-søk med fødselsnummer i URL støttes med hensikt ikke, fordi query strings kan havne i nettleserhistorikk, proxylogger og telemetry. Se [pasient-ID og beskyttet testkontekst](docs/patient-context-testing.md) og [FHIR-eksempler](examples/fhir-queries.md) for de to flytene.

Når DHG leverer et positivt `fetusesVitalSigns[].fosterId`, opprettes en separat minimal fetus `Patient`. Fetus-spesifikke Observations beholder mor som `subject` og refererer til fosteret med `focus`. Fetus Patient har ingen NIN, name, gender, birthDate eller identifier; den kan leses med samme maternal `X-Patient-Context` via `GET /fhir/Patient/{fetus-id}`. Se [DHG→FHIR-ressursmapping](docs/dhg-fhir-resource-mapping.md).

Alle FHIR-svar har `application/fhir+json`. Søk uten treff returnerer en tom `searchset`-Bundle. Feil returneres som `OperationOutcome`. Fasaden tilbyr med hensikt ikke `$populate`.

FHIR terminology bruker norske NLK-koder (NPU/NOR), SNOMED CT, nasjonale Volven-koder og UCUM units. LOINC beholdes som HL7 interoperability coding der mappingen er entydig; en verifisert norsk coding legges til når en slik finnes. «NorLOINC» er ikke et eget norsk code system, og fasaden publiserer ikke egne clinical codes. Et eksplisitt DHG-felt kan likevel eksponeres source-preserving med `Observation.code.text` og raw boolean, integer, date eller tekst når dette ikke krever clinical inference. Sammensatte felt splittes ikke, fritekst parses ikke, nullable booleans blir ikke `false`, og raw skala- eller count-verdier gis ikke en konstruert betydning eller unit. Felt forblir unsupported når source fact, subject/fetus identity, temporal context eller sikker FHIR resource semantics mangler. Se [mappingmatrisen](docs/mapping.md).

## Forutsetninger

- .NET SDK 9.0.317 eller nyere kompatibel 9.0-SDK
- Go 1.25 eller nyere for lokal bygging/testing av auth-gatewayen
- klient registrert i HelseID Test for token exchange til `nhn:maternity-record`
- API-registrering for fasadens audience og scope
- to private JWK-er: én til `private_key_jwt`, én til DPoP; alternativt kan lokal Development-test bruke HelseID TEST-tokenverktøyet som beskrevet under
- syntetisk testperson som finnes i DHG Test

De faktiske testendepunktene og DHG-kravene er dokumentert av NHN i [DHG miljøer](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/environmentsmd), [DHG autorisasjon](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/authorizationmd) og [HelseID token exchange](https://utviklerportal.nhn.no/informasjonstjenester/helseid/bruksmoenstre-og-eksempelkode/bruk-av-helseid/docs/teknisk-referanse/token_exchange_enmd).

## Konfigurasjon

Ikke legg private nøkler eller fødselsnummer i `appsettings.json`. Bruk secret store, miljøvariabler eller en administrert konfigurasjonsleverandør. Eksempel med miljøvariabler:

```powershell
$env:HelseId__ClientId = "<actor-client-id>"
$env:HelseId__ClientAssertionJwk = "<private-jwk-json>"
$env:HelseId__DPoPJwk = "<annen-private-jwk-json>"
$env:AuthGateway__SharedSecret = "<tilfeldig-hemmelighet-minst-32-bytes>"
$env:PatientContext__PatientIdHmacKey = "<base64-kodet-tilfeldig-hemmelighet-minst-32-bytes>"
$env:PatientContext__TestAliases__synthetic_1__LogicalId = "patient-test-1"
$env:PatientContext__TestAliases__synthetic_1__NationalIdentityNumber = "<syntetisk-fnr>"
```

`PatientContext:PatientIdHmacKey` er påkrevd utenfor `DevelopmentTestMode`. Generer minst 32 tilfeldige byte, Base64-kod dem og oppbevar verdien i en godkjent secret store. Nøkkelen må være stabil mellom instanser og restarter; rotasjon endrer de pseudonyme FHIR-ID-ene og må derfor planlegges. Den må være separat fra gateway credential, Data Protection keys og HelseID private keys.

Alias-konfigurasjon er kun tillatt utenfor Production. Endepunktet `POST /test/patient-context/{alias}` er deaktivert i Production. Normalt bindes en beskyttet kontekst til det autentiserte HelseID-subjektet og kan ikke gjenbrukes av en annen innlogget bruker; Development-testmodus binder den i stedet til et fast konfigurert test-subjekt. En godkjent produksjonsmekanisme for ekstern utstedelse av `X-Patient-Context` er ikke implementert. Dette blokkerer de kontekstbaserte GET-operasjonene i Production, men ikke HelseID-beskyttet POST `_search`.

For et lokalt alias er `LogicalId` den ikke-sensitive FHIR-ID-en operatøren velger, for eksempel `patient-test-1`. Den må følge FHIR `id`-formatet `[A-Za-z0-9.-]{1,64}`, være unik med case-sensitive sammenligning og kan ikke være lik et konfigurert fødselsnummer. Alias-endepunktet returnerer denne verdien som `patientId` og pakker koblingen mellom logisk ID, syntetisk fødselsnummer, subjekt og utløp i `patientContext`. I autentisert POST `_search` genereres i stedet en deterministisk, ikke-reverserbar `Patient.id` med HMAC. Ingen av variantene eksponerer fødselsnummeret som FHIR identifier. Se [pasient-ID og beskyttet testkontekst](docs/patient-context-testing.md) for hele flyten.

### Anonym Swagger mot DHG Test

Som standard er denne modusen bare for lokal testing. Den kan starte i `Development` med `Dhg:Environment=Test`; eksplisitte wildcard-/ikke-loopback-listenere avvises, og forespørsler uten en kjent loopback-peer avvises. Modusen må da ikke publiseres gjennom reverse proxy, tunnel eller port-forwarding. Innkommende Swagger/FHIR-kall er anonyme. Utgående DHG-autorisasjon bruker normalt `client_credentials`, client assertion og DPoP server-side, men kan eksplisitt bruke HelseID TEST-tokenverktøyet i samme mønster som smartOppgave. HelseID-klienten må være godkjent for de DHG Test-operasjonene som skal prøves.

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

For lokal Development er .NET user-secrets tryggere enn å skrive auth key i en fil i repositoryet:

```powershell
dotnet user-secrets set "HelseIdTestToken:AuthKey" "<secret-auth-key>" --project src/PopulationDataFacade.Api
```

Da kan linjen som setter `HelseIdTestToken__AuthKey` utelates fra miljøblokken. Organisasjonsnummer, helsepersonellidentitet, HPR-nummer og rolle må være en sammenhengende syntetisk kombinasjon som er gyldig i NHNs testdata; et vilkårlig organisasjonsnummer kan gi `AUTH-0003` fordi DHG ikke finner organisasjonsnavnet. `/status` bruker et machine-to-machine token, mens `/record` bruker et user token med HPR-identitet, `orgnr_parent`, `orgnr_child`, `nhn-user-role` og `nhn-treatment-facility-name`. Alternativt kan konfigurasjonen leveres som miljøvariabler eller fra en godkjent secret store. Ikke lagre `accessTokenJwt` eller `dPoPProof`: fasaden henter et nytt par for hvert DHG-kall fordi beviset bindes til eksakt HTTP-metode og URL. Ren .NET laster ikke `.env`-filer automatisk; en eventuell lokal `.env` må importeres til prosessmiljøet av utviklerverktøyet.

I Swagger:

1. Kall `POST /test/patient-context/{alias}` med `synthetic_1`.
2. Kopier `patientId` (den konfigurerte logiske FHIR-ID-en, ikke fødselsnummeret) og `patientContext` fra svaret.
3. Kall ønsket FHIR-operasjon, bruk nøyaktig denne `patientId`-verdien i route/query, og lim `patientContext` inn i `X-Patient-Context`-feltet Swagger viser.

Som en lokal testforenkling kan de fire POST `_search`-operasjonene brukes direkte med et fødselsnummer som allerede finnes i `PatientContext:TestAliases`. Fødselsnummeret legges i form body, ikke i URL, og Swagger viser derfor ikke `X-Patient-Context` for disse operasjonene. De returnerte FHIR-ressursene bruker fortsatt konfigurert `LogicalId` og inneholder aldri fødselsnummeret.

De samme POST-operasjonene finnes i autentisert drift og Production. Der kreves HelseID `population.read`, `PatientContext:PatientIdHmacKey` og et innkommende subject-token; `TestAliases` brukes ikke. DHGs consent-, personstatus- og active-record-kontroller kjøres fortsatt før FHIR-mapping.

Modusen avvises ved oppstart utenfor lokal Development og alltid mot annet enn DHG Test. Den må aldri aktiveres i Staging, QA eller Production.

### Autentisert lokal gateway

Autentisert kjøring består av to prosesser. Start først det private API-et på loopback-port 8081 med `DevelopmentTestMode` avslått, `ReverseProxy__ForwardedHeadersEnabled=true`, HelseID/DHG-konfigurasjon og `AuthGateway__SharedSecret` satt. Start deretter `auth-gateway` på loopback-port 8080 med den samme hemmeligheten, fasadens audience/scope, kanonisk ekstern host og eksplisitt replay-store. For én lokal instans kan `AUTH_GATEWAY_REPLAY_STORE=memory` og `AUTH_GATEWAY_SINGLE_REPLICA=true` brukes.

Gatewayen terminerer ikke TLS. Plasser derfor en betrodd lokal HTTPS-reverse proxy foran `http://127.0.0.1:8080`, bevar den kanoniske `Host`-verdien, og la DPoP-bevisets `htu` peke på den eksterne HTTPS-URL-en. Port 8080 må aldri eksponeres direkte over et ubeskyttet nett. Full konfigurasjon og sikkerhetskrav står i [HelseID-oppsettet](docs/helseid-setup.md).

### Swagger i Production

Swagger og begge OpenAPI-dokumentene er deaktivert som standard i Production. Dersom de er nødvendige i et kontrollert produksjonsmiljø, må de aktiveres eksplisitt:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:Swagger__EnabledInProduction = "true"
```

Når dette er aktivert, krever `/swagger`, `/swagger/v1/swagger.json` og `/openapi/v1.json` et gyldig HelseID DPoP access-token som oppfyller den samme `population.read`-policyen som FHIR-operasjonene. En manglende identitet får `401`, og en autentisert identitet uten konfigurert fasadescope får `403`. Den samme produksjonsgrensen brukes dersom enten host-miljøet eller `Dhg:Environment` er `Production`; `DevelopmentTestMode` kan aldri brukes mot DHG Production.

En vanlig Swagger UI i nettleseren er ikke i seg selv en HelseID/DPoP-klient. Produksjons-UI må derfor ligge bak en godkjent HelseID-aware backend/reverse proxy som håndterer innlogging og DPoP server-side for alle UI- og API-kall. Uten en slik komponent bør bare OpenAPI-dokumentet hentes med et DPoP-kompatibelt verktøy; access-token skal ikke limes inn i nettleseren.

Konfigurasjonen valideres ved oppstart. `Dhg:Environment` må være `Test` eller `Production`; ukjente verdier og blandede Test/Production-endepunkter avvises. DHG audience/scope er låst til dokumenterte verdier, facade scope må være satt, og autentisert drift krever en gyldig Base64-kodet `PatientIdHmacKey` på minst 32 byte. Normalflyten krever asymmetrisk privat JWK-materiale. TEST-tokenverktøyet kan bare erstatte disse nøklene når både `DevelopmentTestMode` og `HelseIdTestToken` er eksplisitt aktivert mot DHG Test.

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

Repositoryet bruker .NET 9. `dotnet run` utfører normalt både restore og build automatisk. Første kjøring kan derfor ta merkbart lengre tid mens NuGet-pakker lastes ned, alle prosjektene kompileres og Windows eventuelt skanner nye build-filer. En påfølgende restore skal normalt være rask når pakkene allerede finnes i lokal cache.

Kjør restore eksplisitt én gang for å skille NuGet-steget fra kompileringen:

```powershell
dotnet --version
dotnet restore PopulationDataFacade.slnx --locked-mode --verbosity minimal
dotnet run --project src/PopulationDataFacade.Api --no-restore
```

`dotnet --version` skal vise en `9.0.x`-versjon. Når API-et allerede er bygget og kildekoden er uendret, kan også build-steget hoppes over:

```powershell
dotnet run --project src/PopulationDataFacade.Api --no-restore --no-build
```

Ikke bruk `--no-build` etter endringer i kode, prosjektfiler eller pakker. Hvis restore blir stående lenge på `Determining projects to restore...`, kjør kommandoen på nytt med `--verbosity normal` og kontroller tilgangen til den konfigurerte NuGet-kilden. `NU1900` eller `Unable to load the service index` betyr at NuGet ikke når pakke- eller sårbarhetstjenesten; kontroller nettverk, DNS og eventuell proxy. Tiden etter en fullført restore er build-tid, ikke restore-tid.

Kjør API-et med den eksplisitte anonyme Development-testkonfigurasjonen ovenfor, eller bruk den autentiserte to-prosessflyten. Launch-profilen starter API-et normalt på `https://localhost:7184`; dette er en direkte lokal Development-adresse, ikke den offentlige adressen for autentisert drift. Swagger UI og OpenAPI-dokumentet er tilgjengelig uten innlogging i ikke-produksjonsmiljøer på henholdsvis `/swagger` og `/openapi/v1.json`. I Production er de deaktivert som standard og HelseID-beskyttet når de aktiveres eksplisitt.

Lokal Development deaktiverer Windows EventLog logging i `appsettings.Development.json` og beholder standard Console/Debug providers. Dette hindrer at en kontrollert FHIR-feil blir avbrutt dersom utviklerprosessen mangler skrivetilgang til Windows Event Log. Swagger skal derfor vise den faktiske `OperationOutcome`-responsen, for eksempel `404`, i stedet for bare `Failed to fetch`.

## Drift

- Liveness: `/health/live`
- Readiness: `/health/ready` (foreløpig en grunn prosess-sjekk; ingen DHG/HelseID-avhengighet verifiseres)
- korrelasjons-ID: responsheader `X-Correlation-ID`
- OpenTelemetry: ASP.NET Core, utgående HTTP, DHG-målinger og token-exchange-spor
- OTLP-eksport aktiveres når `OTEL_EXPORTER_OTLP_ENDPOINT` er satt

Logger inneholder aldri access-token, privat nøkkel, fødselsnummer eller klinisk payload. DHG-kall bruker `nhn-patient-nin` kun som utgående header. Kun idempotente GET-kall retries ved timeout, 429 og relevante 5xx-feil; `Retry-After` respekteres.

Se [pasient-ID og beskyttet testkontekst](docs/patient-context-testing.md), [arkitektur](docs/architecture.md), [DHG→FHIR-ressursmapping](docs/dhg-fhir-resource-mapping.md), [mappingmatrise](docs/mapping.md), [DHG-kildeliste](docs/dhg-source-inventory.md), [populasjonsdekning](docs/dhg-population-coverage.md), [SDC-bruk](docs/sdc-usage.md), [HelseID-oppsett](docs/helseid-setup.md), [sikkerhetsarkitektur](docs/security-architecture.md), [drift](docs/operations.md) og [FHIR-eksempler](examples/fhir-queries.md) før produksjonssetting.

## Bevisste avgrensninger

- ingen demografikilde, fastlegekilde, Grunndata-adapter eller annen kilde enn DHG
- ingen Questionnaire/QuestionnaireResponse eller linkId-avhengighet
- ingen inferert provosert abort, diagnose, legemiddelnavn eller annen klinisk betydning fra fritekst
- ingen historisk rekonstruksjon utover eksplisitte DHG-felter
- ingen persistent caching av kliniske DHG-data
- `birthStatus` og kontakt-/demografifelter eksponeres ikke i første FHIR-flate; begrunnelsen står i mappingmatrisen

## Lisens

Repositoryets originale kildekode er lisensiert under [MIT-lisensen](LICENSE). Tredjepartsavhengigheter beholder sine egne vilkår; se [tredjepartsavhengigheter og lisenser](THIRD-PARTY-NOTICES.md). CI avviser runtime-avhengigheter utenfor prosjektets gjennomgåtte MIT-, Apache-2.0-, BSD-2-Clause- og BSD-3-Clause-lisenser.
