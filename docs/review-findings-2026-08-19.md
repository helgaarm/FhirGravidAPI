# Review findings — 2026-08-19

Three independent, report-only repository reviewers were run against the approved Word brief, implementation, tests, configuration and documentation:

- documentation drift reviewer;
- full-stack code reviewer;
- test-gap reviewer.

## Addressed in this pass

- Added the three reusable reviewer definitions under `.codex/agents`.
- Removed dynamic DHG record IDs from custom telemetry and filtered generic DHG URL spans.
- Bound protected patient contexts to the authenticated HelseID subject and added cross-subject replay coverage.
- Rejected unsupported DHG environment names and empty facade scope at startup.
- Added timeout retry and both `Retry-After` forms.
- Preserved historical appointment gestational age while emitting exactly one latest value.
- Removed invented local strings from the NLK namespace and corrected hemoglobin to the currently verified `g/dL` unit.
- Avoided inferred Patient, Observation and Encounter status claims.
- Corrected vital-sign categories, fetal-heart-rate quantity typing and standalone fetal RhD result date.
- Made 401/403 responses consistently return FHIR `OperationOutcome`.
- Added no-store/security headers and configurable patient-context header handling to the test client.
- Added active-record/consent, privacy telemetry, retry, mapping, configuration, authorization and replay regression tests.
- Added the documentation artifacts and query examples required by the brief.

## Remaining release blockers

- No approved production patient-context authority or interoperability protocol exists.
- No opt-in real HelseID Test → token exchange → DHG `/status` → `/record` smoke test exists.
- Clinical terminology ownership has not approved every code, unit, FHIR datatype/category/status and consumer meaning.
- Meaningful readiness, shared encrypted Data Protection storage, trusted proxy/canonical URL configuration, exact host allowlists, locked restore/CI security gates and immutable image policy remain deployment work.
- The test client remains a generic resource browser rather than the full eight-area workflow requested by the brief.

## Verification

After the fixes, a serialized solution build completed with zero warnings and zero errors. All 35 tests passed: 3 contract, 7 integration and 25 unit tests. No real external HelseID/DHG call was made.
