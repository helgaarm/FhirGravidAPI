# Bruk av SDC population

Denne fasaden er en generisk read-only FHIR population source. Den har med hensikt ingen Questionnaire IDs, linkIds, QuestionnaireResponse-logikk eller `$populate` endpoint. En SDC engine avgjør hvilke stabile fakta som skal etterspørres.

## Query pattern

```text
GET /fhir/Observation?patient={logical-patient-id}&code={system}|{code}
```

Klienten må sende:

- et gyldig HelseID DPoP access token med konfigurert facade read scope
- beskyttet, kortlivet patient context i konfigurert header
- samme logical patient ID i query og beskyttet context

For manuell testing tillater eksplisitt test mode anonyme Swagger/FHIR requests og flytter HelseID authentication til fasadens server-side DHG client. Modusen er begrenset til loopback-only Development med DHG Test. Dette er ikke et SDC deployment pattern og kan ikke aktiveres i Staging, QA eller Production.

Når en URI bygges, skal vertical bar representeres med percent-encoding som `%7C`. Eksempel:

```text
/fhir/Observation?patient=patient-test-1&code=http%3A%2F%2Floinc.org%7C39156-5
```

## Tolkning av resultater

- `Bundle.total=0` betyr at faktumet ikke er registrert i active DHG record; det betyr ikke false.
- `valueBoolean=false` er en eksplisitt negativ verdi og må ikke slås sammen med fravær.
- Unsupported concepts finnes ikke i fasadens publiserte coverage contract; consumers må ikke behandle dem som queried-but-empty.
- Gestational age bruker `http://loinc.org|18185-9`, UCUM `d` og refererer til appointment Encounter. Consumer velger nyeste `effectiveDateTime` når bare current value trengs.
- Errors er FHIR `OperationOutcome`; consumers skal velge branch basert på HTTP status og `issue.code`, ikke parse diagnostics text.

Bruk [coverage contract](dhg-population-coverage.md) og [kjøreklare eksempler](../examples/fhir-queries.md) når en consumer konfigureres.
