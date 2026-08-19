# Repository invariants

- DHG API is the only runtime data source.
- Do not add demographics, GP, Grunndata or other source adapters.
- FHIR API layer must not know DHG JSON paths.
- Questionnaire IDs/linkIds must not appear in source or FHIR mapping logic.
- Never infer unsupported clinical facts from unrelated DHG fields or free text.
- Never collapse nullable booleans into false.
- Use the current active DHG record for current-pregnancy population.
- No runtime DHG mocks or fallback data.
- No persistent caching of DHG clinical data without an explicit architecture decision.
- Never log tokens, NIN, private keys or clinical payloads.
- Run `dotnet build` and `dotnet test` before completing work.
