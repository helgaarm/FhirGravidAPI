# DHG-funn og beslutninger

Gjennomgangsdato: 2026-08-19.

Denne loggen skiller verifiserte implementeringsbeslutninger fra uavklarte kliniske spørsmål og spørsmål om ekstern integrasjon.

## Verifiserte beslutninger

- Begge DHG source-feltene for hemoglobin refererer til NOR05172. Fasaden bruker separate lokale trimester concepts slik at population queries forblir entydige, mens verdiene bruker UCUM `g/dL` basert på NHNs gjeldende laboratorieeksempel.
- HBV surface antigen, HBV core antibody og toxoplasmose er eksplisitte DHG booleans, men source-feltet identifiserer ikke én entydig analysis code. De bruker derfor facade-owned codes i stedet for oppdiktede verdier i NLK namespace.
- Hver appointment beholder sin egen gestational-age Observation; bare den siste relevante appointment produserer `recorded-gestational-age`.
- `rhesusDNegative.dateForResult` eksponeres som et separat date-faktum og som temporal context for fosterets RhD-resultat.
- Nullable booleans beholder alle tre tilstander: true, false og fraværende.

## Åpne funn / release gates

- Clinical terminology owner må godkjenne alle codes, units, datatypes og FHIR category/status før ekstern promotering til DHG Test.
- Faktiske DHG Test `/status`- og `/record`-payloads er ikke verifisert med en opt-in end-to-end test.
- HelseID discovery, token exchange, DPoP nonce-håndtering og DHG resource calls er ikke kjørt med godkjente eksterne credentials.
- Det finnes ingen godkjent production patient-context issuer/trust protocol.
- QA er ikke en støttet `Dhg:Environment`-verdi før eksakte endpoints og validation rules er godkjent.

## Kontrollerte autoritative referanser

- [NHN DHG resource model](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/resourcesmd/)
- [NHN laboratory message example showing NOR05172 with g/dL](https://utviklerportal.nhn.no/no/informasjonstjenester/kjernejournal/pasientens-proevesvar/pps-documentation/docs/svarmeldingmd)

Kontroller disse kildene på nytt og registrer datoen hver gang terminologi eller DHG contract endres.
