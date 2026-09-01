# Pasient-ID og beskyttet pasientkontekst

Fasaden skiller FHIR-pasient-ID fra fødselsnummer. Fødselsnummer returneres ikke som `Patient.id` eller `Patient.identifier`.

| Verdi | Bruk |
|---|---|
| Alias, for eksempel `synthetic_1` | Lokalt navn på en konfigurert syntetisk testpasient |
| `LogicalId`, for eksempel `patient-test-1` | FHIR `Patient.id` i lokal testmodus |
| Fødselsnummer | Sendes bare til DHG i påkrevd header eller mottas i POST `_search`-kroppen |
| `patientContext` | Kortlivet, beskyttet binding mellom logisk ID, fødselsnummer, subjekt og utløp |
| Pseudonym pasient-ID | FHIR `Patient.id` fra HMAC ved autentisert POST `_search` |
| Foster-ID | Pseudonym FHIR-ID fra morens logiske ID, aktiv DHG-post og positiv `fosterId` |

## Lokal test med alias

Konfigurer en godkjent syntetisk DHG Test-pasient utenfor kildekoden:

```powershell
$project = "src/PopulationDataFacade.Api"
dotnet user-secrets set "PatientContext:TestAliases:synthetic_1:LogicalId" "patient-test-1" --project $project
dotnet user-secrets set "PatientContext:TestAliases:synthetic_1:NationalIdentityNumber" "<syntetisk-testfødselsnummer>" --project $project
```

`LogicalId` må følge FHIR-formatet `[A-Za-z0-9.-]{1,64}`, være unikt ved sammenligning som skiller mellom store og små bokstaver, og være forskjellig fra alle konfigurerte fødselsnumre.

I lokal `DevelopmentTestMode` utstedes pasientkonteksten slik:

```powershell
$facadeBase = "https://localhost:7184"
$selection = Invoke-RestMethod `
  -Method Post `
  -Uri "$facadeBase/test/patient-context/synthetic_1"

$headers = @{
  "X-Patient-Context" = $selection.patientContext
  Accept = "application/fhir+json"
}

Invoke-RestMethod `
  -Uri "$facadeBase/fhir/Patient/$($selection.patientId)" `
  -Headers $headers
```

Endepunktet returnerer:

```json
{
  "patientId": "patient-test-1",
  "patientContext": "<kortlivet-beskyttet-verdi>"
}
```

Standard levetid er ti minutter. Følgende GET-operasjoner krever samme `patientId` i ruten eller søket som i pasientkonteksten:

```text
GET /fhir/Patient/{id}
GET /fhir/Observation?patient={id}
GET /fhir/Encounter?patient={id}
GET /fhir/CareTeam?patient={id}
```

En foster-ID som er returnert i `Observation.focus`, leses med morens pasientkontekst. Bare fostre i det samme DHG-øyeblikksbildet godtas.

## Autentisert POST-søk

Disse operasjonene mottar fødselsnummer i en `application/x-www-form-urlencoded`-kropp og bruker ikke `X-Patient-Context`:

```text
POST /fhir/Patient/_search
  identifier=<fødselsnummer>

POST /fhir/Observation/_search
  patient.identifier=<fødselsnummer>
  code=<system|kode>
  category=<kategori>
  date=<prefiks><yyyy-MM-dd>

POST /fhir/Encounter/_search
  patient.identifier=<fødselsnummer>

POST /fhir/CareTeam/_search
  patient.identifier=<fødselsnummer>
```

Utenfor lokal `DevelopmentTestMode` kreves HelseID-token, DPoP-bevis og tilgangsomfanget `population.read`. `PatientContext:PatientIdHmacKey` må være en Base64-kodet hemmelighet på minst 32 byte. Fasaden bruker nøkkelen til en pseudonym `Patient.id`; fødselsnummeret returneres ikke.

DPoP-beviset er bundet til HTTP-metoden og URL-en og må være nytt for hvert kall.

I lokal `DevelopmentTestMode` er POST-søkene anonyme, men fødselsnummeret må samsvare med ett konfigurert testalias. Returnerte ressurser bruker aliasets `LogicalId`.

```powershell
$approvedSyntheticNin = Read-Host "Godkjent syntetisk fødselsnummer"
Invoke-RestMethod `
  -Method Post `
  -Uri "$facadeBase/fhir/Observation/_search" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{ "patient.identifier" = $approvedSyntheticNin }
```

Fødselsnummer skal ikke legges i en GET-URL.

## Vanlige svar

| Respons | Betydning |
|---:|---|
| `400` | Pasientkonteksten, skjemaet eller fødselsnummerformatet er ugyldig |
| `401` | HelseID-token eller DPoP-bevis mangler eller er ugyldig |
| `403` | Tilgangsomfang eller DHG-samtykke mangler |
| `404` | Alias, pasient, aktivt helsekort eller samsvarende pasient-ID finnes ikke |
| `500` | Lokal konfigurasjonsfeil |
| `503` | HelseID eller DHG er utilgjengelig, eller DHG-kontrakten brytes |

Pasientkonteksten skal behandles som sensitiv og ikke legges i kildekontroll, logger, saker eller chat.

## Implementert avgrensning

Aliasendepunktet finnes ikke i produksjon. Repositoriet implementerer ingen produksjonsutsteder for `X-Patient-Context`, og de kontekstbaserte GET-operasjonene har derfor ingen komplett produksjonsflyt. Den HelseID-beskyttede POST `_search`-flyten er implementert separat.
