# DHG-kontrakt for population coverage

Fasaden eksponerer bare `Patient`, `Observation` og `Encounter`. Den implementerer ikke `$populate`, Questionnaire processing, demographics lookup, GP lookup, Grunndata eller andre kliniske sources.

## Consumer contract

- `Patient/{id}` er minimal og inneholder ikke NIN, navn, adresse, fødselsdato, GP eller kontaktinformasjon.
- Vanlig Observation search krever `patient={logical-id}` og aksepterer valgfritt ett `code={system}|{code}` token. Lokal `DevelopmentTestMode` har i tillegg POST `_search` med `patient.identifier` i form body for en konfigurert syntetisk testperson.
- En manglende/null DHG-verdi produserer ingen Observation. Eksplisitt `false` produserer `valueBoolean: false`.
- `metadata.enteredInError=true` produserer ingen FHIR resource.
- `meta.lastUpdated` kommer fra DHG source metadata når de er tilgjengelige.
- `recorded-gestational-age` forekommer maksimalt én gang og representerer den siste daterte appointment uten error som inneholder uke- eller dagdata.
- `gestational-age-at-appointment` beholder datert appointment-historikk.
- Etter vellykket patient selection returnerer search uten kliniske treff en FHIR `searchset` Bundle med `total=0`. En ukjent lokal syntetisk identifier returnerer i dagens test-support-kontrakt `404`.

## Stabile facade concepts

Facade-owned concepts bruker `urn:nhn:population-data`. Eksempler:

- `needs-language-interpreter`
- `due-date-last-period`
- `due-date-ultrasound`
- `pre-pregnancy-bmi`
- `recorded-gestational-age`
- `gestational-age-at-appointment`
- `hemoglobin-first-trimester`
- `hemoglobin-third-trimester`
- `hbv-s-antigen-positive`
- `hbv-core-antibody-positive`
- `toxoplasmosis-positive`
- `fetus-rhd-result-date`

Fullstendig gjeldende feltklassifisering finnes i [mapping.md](mapping.md). Query-eksempler finnes i [examples/fhir-queries.md](../examples/fhir-queries.md).

## Eksplisitt unsupported eller partial

- Legemiddelnavn eller dose infereres ikke fra en medication note.
- Indusert abort, diagnose eller andre kliniske fakta beregnes ikke som residual og trekkes ikke ut fra free text.
- Contact/demographic- og birth-status-data eksponeres ikke i første API surface.
- Ukjente source-felt tolereres, men eksponeres ikke automatisk.
- Blodtrykk eksponeres bare når dokumentert `systolic/diastolic`-format kan parses sikkert.

Godkjenning av terminology og units fra utpekt clinical owner er fortsatt en release gate, også når implementasjonstestene passerer.
