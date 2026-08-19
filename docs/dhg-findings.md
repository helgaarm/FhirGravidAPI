# DHG findings and decisions

Review date: 2026-08-19.

This log separates verified implementation decisions from unresolved clinical or external-integration questions.

## Verified decisions

- Both DHG hemoglobin source fields refer to NOR05172. The facade uses separate local trimester concepts so population queries remain unambiguous, while the values use UCUM `g/dL` based on the current NHN laboratory example.
- HBV surface antigen, HBV core antibody and toxoplasmosis are explicit DHG booleans, but the source field does not identify one unambiguous analysis code. They therefore use facade-owned codes instead of invented values in the NLK namespace.
- Every appointment keeps its own gestational-age observation; only the latest relevant appointment produces `recorded-gestational-age`.
- `rhesusDNegative.dateForResult` is exposed as a separate date fact and as temporal context for the fetal RhD result.
- Nullable booleans retain all three states: true, false, and absent.

## Open findings / release gates

- Clinical terminology ownership must approve every code, unit, datatype and FHIR category/status before external DHG Test promotion.
- The actual DHG Test `/status` and `/record` payloads have not been verified by an opt-in end-to-end test.
- HelseID discovery, token exchange, DPoP nonce behavior and DHG resource calls have not been exercised with approved external credentials.
- No approved production patient-context issuer/trust protocol exists.
- QA is not a supported `Dhg:Environment` value until exact endpoints and validation rules are approved.

## Authoritative references checked

- [NHN DHG resource model](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/resourcesmd/)
- [NHN laboratory message example showing NOR05172 with g/dL](https://utviklerportal.nhn.no/no/informasjonstjenester/kjernejournal/pasientens-proevesvar/pps-documentation/docs/svarmeldingmd)

Recheck these sources and record the date whenever terminology or the DHG contract changes.
