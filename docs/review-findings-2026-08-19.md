# Review-funn — 2026-08-19

Tre uavhengige report-only repository reviewers ble kjørt mot godkjent Word-brief, implementasjon, tester, konfigurasjon og dokumentasjon:

- documentation drift reviewer
- full-stack code reviewer
- test-gap reviewer

## Håndtert i denne gjennomgangen

- La til de tre gjenbrukbare reviewer-definisjonene under `.codex/agents`.
- Fjernet dynamiske DHG record IDs fra custom telemetry og filtrerte generiske DHG URL spans.
- Bandt beskyttede patient contexts til autentisert HelseID subject og la til cross-subject replay coverage.
- Avviste unsupported DHG environment names og tom facade scope ved startup.
- La til timeout retry og begge `Retry-After`-formene.
- Bevarte historisk gestational age per appointment samtidig som nøyaktig én siste verdi emittes.
- Fjernet oppdiktede lokale strings fra NLK namespace og korrigerte hemoglobin til gjeldende verifiserte `g/dL` unit.
- Unngikk infererte status claims for Patient, Observation og Encounter.
- Korrigerte vital-sign categories, quantity typing for fetal heart rate og separat resultatdato for fosterets RhD.
- Sørget for at 401/403-responses konsekvent returnerer FHIR `OperationOutcome`.
- La til no-store/security headers og konfigurerbar håndtering av patient-context header i testklienten.
- La til regression tests for active record/consent, privacy telemetry, retry, mapping, konfigurasjon, authorization og replay.
- La til documentation artifacts og query-eksempler som briefen krevde.

## Gjenværende release blockers

- Det finnes ingen godkjent production patient-context authority eller interoperability protocol.
- Det finnes ingen opt-in reell HelseID Test → token exchange → DHG `/status` → `/record` smoke test.
- Clinical terminology owner har ikke godkjent alle codes, units, FHIR datatypes/categories/statuses og consumer meaning.
- Meaningful readiness, delt kryptert Data Protection storage, trusted proxy/canonical URL-konfigurasjon, eksakte host allowlists, locked restore/CI security gates og immutable image policy gjenstår som deployment-arbeid.
- Testklienten er fortsatt en generisk resource browser, ikke den komplette workflowen med åtte områder som briefen etterspurte.

## Verifikasjon

Etter rettingene fullførte et serialisert solution build med null warnings og null errors. Alle 35 tester passerte: 3 contract-, 7 integration- og 25 unit tests. Ingen reelle eksterne HelseID/DHG calls ble utført.
