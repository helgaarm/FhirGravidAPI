# Mapping fra DHG API til FHIR R4-ressurser

Dette dokumentet beskriver ressursene som fasaden oppretter. Feltklassifisering står i [mappingmatrisen](mapping.md), og full sporing fra DHG-attributt til FHIR står i [attributtmappingen](dhg-facade-attribute-mapping.md).

## Valg av DHG-post

| DHG-verdi | Kontroll | Feil |
|---|---|---:|
| `GET /status` | Henter samtykke, personstatus og `latestRecordId` | `OperationOutcome` |
| `hasGivenConsent` | Må være `true` | 403 |
| `deceased` | Må ikke være `true` | 403 |
| `hasActiveMaternityRecord` | Må være `true` | 404 |
| `latestRecordId` | Må være en gyldig, ikke-tom UUID | 503 |
| `GET /record/{latestRecordId}` | Eneste kliniske datakilde | 503 |
| `metadata.recordId` | Må samsvare med `latestRecordId` | 503 |
| `recordStatus.status` | Må være `ACTIVE` | 404 |

Fasaden bruker ingen alternativ datakilde og har ikke varig klinisk mellomlagring.

## Ressurser

| Ressurs | Antall | Kilde |
|---|---:|---|
| `CapabilityStatement` | 1 | Statisk beskrivelse av FHIR-flaten |
| `Patient` | 1..* | Morens logiske ID og eventuelle foster-ID-er fra positiv `fosterId` |
| `Observation` | 0..* | Eksplisitte DHG-felt med sikker betydning |
| `Encounter` | 0..* | Konsultasjoner som ikke er markert som feil |
| `CareTeam` | 0..1 | Fastlege, jordmor, helsestasjon og fødeinstitusjon fra `pointsOfContact` |
| `Bundle` | 1 | Resultat fra FHIR-søk |
| `OperationOutcome` | 0..1 | Kontrollert feil |

## Felles regler

- `metadata.enteredInError=true` utelater hele ressursen.
- `null` gir ingen FHIR-verdi. Eksplisitt `false` beholdes.
- `metadata.lastUpdated` mappes til `meta.lastUpdated`.
- Måledato mappes til `effectiveDateTime` med dagspresisjon.
- Observasjoner bruker mor som `subject`.
- Fosterobservasjoner bruker `focus` bare når en positiv `fosterId` finnes.
- Konsultasjonsbaserte observasjoner refererer til kildens `Encounter`, også når dato mangler.
- `Observation` og `Encounter` får status `unknown` fordi DHG ikke leverer en entydig FHIR-status.
- Kliniske koder kommer fra SNOMED CT, NLK, Volven eller LOINC. Måleenheter bruker UCUM.
- Ukjente kodeverk, enumverdier og fritekst oversettes ikke automatisk.
- Alle observasjoner bruker basisressursen FHIR R4 `Observation` uten egne `meta.profile`-verdier.

## Patient og foster

Morens `Patient.id` kommer fra pasientkonteksten eller HMAC-pseudonymiseringen. Den er aldri et fødselsnummer. `mother.language` mappes til `Patient.communication.language`, og `mother.needsLanguageInterpreter` bruker HL7-utvidelsen `patient-interpreterRequired`.

En positiv `fetusesVitalSigns[].fosterId` gir en pseudonym foster-ID basert på morens logiske ID, aktiv DHG-post og `fosterId`. Fosterressursen inneholder bare `id` og eventuell `meta.lastUpdated`.

## CareTeam

Fastlege og jordmor mappes til inneholdte `Practitioner`, `Organization` og `PractitionerRole`. HPR-nummer og fastlegens organisasjonsnummer publiseres bare når DHG leverer dem. Helsestasjon og fødeinstitusjon mappes til inneholdte `Organization`-ressurser uten konstruerte identifikatorer.

## Søk

GET-søk bruker logisk pasient-ID og `X-Patient-Context`. Observation støtter filtrene `code`, `category` og dagspresis `date`.

POST `_search` mottar fødselsnummer i forespørselskroppen. I autentisert drift kreves HelseID og pseudonym `Patient.id` fra HMAC. Lokal `DevelopmentTestMode` bruker et konfigurert testalias. Pasientvalget endrer ikke den kliniske mappingen.

Et søk uten treff returnerer en `searchset`-`Bundle` med `total=0`.
