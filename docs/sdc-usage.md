# SDC population usage

This facade is a generic read-only FHIR population source. It deliberately has no Questionnaire IDs, linkIds, QuestionnaireResponse logic, or `$populate` endpoint. An SDC engine decides which stable facts to request.

## Query pattern

```text
GET /fhir/Observation?patient={logical-patient-id}&code={system}|{code}
```

The client must send:

- a valid HelseID DPoP access token with the configured facade read scope;
- the protected, short-lived patient context in the configured header;
- the same logical patient ID in the query and protected context.

The vertical bar should be percent-encoded as `%7C` when constructing a URI. Example:

```text
/fhir/Observation?patient=patient-test-1&code=urn%3Anhn%3Apopulation-data%7Cpre-pregnancy-bmi
```

## Result interpretation

- `Bundle.total=0` means the fact is not registered in the active DHG record; it does not mean false.
- `valueBoolean=false` is an explicit negative value and must not be collapsed into absence.
- Unsupported concepts are absent from the facade's published coverage contract; consumers must not treat them as queried-but-empty.
- `recorded-gestational-age` has at most one result. Historical values use `gestational-age-at-appointment` and reference their Encounter.
- Errors are FHIR `OperationOutcome`; consumers should branch on HTTP status and `issue.code`, not parse diagnostics text.

Use [the coverage contract](dhg-population-coverage.md) and [ready-to-run examples](../examples/fhir-queries.md) when configuring a consumer.
