# DHG source inventory

This inventory describes the DHG read model used by the facade. DHG remains the only runtime source. The API first reads `/status`, requires explicit consent and an active record, then reads `/record/{latestRecordId}` and verifies matching identity and `ACTIVE` status.

| DHG resource area | DTO support | Exposed in the first FHIR surface |
|---|---|---|
| `mother` | Yes | Preferred language and interpreter need only |
| `currentPregnancy` | Yes | Explicit dates, fetal count, assisted conception, counselling flags |
| `previousPregnancies` | Yes | Explicit counters and unparsed note |
| `geneticDisorders` | Yes | Explicit nullable booleans and unparsed note |
| `medicalConditions` | Yes | Explicit nullable booleans and unparsed note |
| `medication` | Yes | Frequency, allergy and folate facts; no inferred medicine names |
| `lifestyleFactors` | Yes | Explicit coded stimuli and frequency components |
| `clinicalTests` | Yes | Explicit results with conservative facade/authoritative terminology |
| `rhesusDNegative` | Yes | Consent, result, result date and prophylaxis |
| `vitalMeasurementsBeforePregnancy` | Yes | Height, pre-pregnancy weight and BMI |
| `symphysisFundalHeights` | Yes | Measurement, date and pregnancy week |
| `antenatalAppointments` | Yes | Encounter dates and explicit measurements/findings |
| `pointsOfContact` | Yes | Not exposed; the facade is not a directory/demographics source |
| `birthStatus` | Yes | Not exposed in the active-pregnancy first release |

Every resource DTO accepts unknown JSON properties for forward compatibility. Exact property names remain case-sensitive, including `bMI`. Resources marked `metadata.enteredInError=true` are excluded. See [the mapping matrix](mapping.md) for field-level behavior.

Source reference: [NHN DHG resource documentation](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/resourcesmd/). Revalidate it before a contract or terminology upgrade.
