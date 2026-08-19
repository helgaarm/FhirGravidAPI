# DHG population coverage contract

The facade exposes only `Patient`, `Observation`, and `Encounter`. It does not implement `$populate`, Questionnaire processing, demographics lookup, GP lookup, Grunndata, or another clinical source.

## Consumer contract

- `Patient/{id}` is minimal and contains no NIN, name, address, birth date, GP, or contact information.
- Observation search requires `patient={logical-id}` and optionally accepts one `code={system}|{code}` token.
- A missing/null DHG value produces no Observation. Explicit `false` produces `valueBoolean: false`.
- `metadata.enteredInError=true` produces no FHIR resource.
- `meta.lastUpdated` comes from DHG source metadata when available.
- `recorded-gestational-age` occurs at most once and represents the last dated, non-error appointment containing week or day data.
- `gestational-age-at-appointment` retains dated appointment history.
- Empty searches return a FHIR `searchset` Bundle with `total=0`.

## Stable facade concepts

Facade-owned concepts use `urn:nhn:population-data`. Examples include:

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

The complete current field classification is in [mapping.md](mapping.md). Query examples are in [examples/fhir-queries.md](../examples/fhir-queries.md).

## Explicitly unsupported or partial

- No medicine name or dose is inferred from a medication note.
- No induced abortion, diagnosis, or other clinical fact is calculated as a residual or extracted from free text.
- Contact/demographic and birth-status data are not exposed in the first surface.
- Unknown source fields are tolerated but not automatically exposed.
- Blood pressure is exposed only when the documented `systolic/diastolic` form is safely parseable.

Terminology and unit approval by the designated clinical owner remains a release gate even where implementation tests pass.
