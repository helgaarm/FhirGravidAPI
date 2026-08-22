# DHG source inventory

Denne oversikten beskriver DHG read model som fasaden bruker. DHG er fortsatt eneste runtime source. API-et leser først `/status`, krever eksplisitt consent og en active record, leser deretter `/record/{latestRecordId}` og verifiserer samsvarende identitet og `ACTIVE` status.

| DHG resource area | DTO support | Eksponert i første FHIR surface |
|---|---|---|
| `mother` | Ja | Bare preferred language og behov for tolk |
| `currentPregnancy` | Ja | Eksplisitte dates, fetal count, assisted conception og counselling flags |
| `previousPregnancies` | Ja | Eksplisitte counters og uparset note |
| `geneticDisorders` | Ja | Eksplisitte nullable booleans og uparset note |
| `medicalConditions` | Ja | Eksplisitte nullable booleans og uparset note |
| `medication` | Ja | Frequency-, allergy- og folate-fakta; ingen infererte legemiddelnavn |
| `lifestyleFactors` | Ja | Eksplisitte coded stimuli og frequency components |
| `clinicalTests` | Ja | Eksplisitte resultater med konservativ facade/authoritative terminology |
| `rhesusDNegative` | Ja | Consent, resultat, resultatdato og prophylaxis |
| `vitalMeasurementsBeforePregnancy` | Ja | Høyde, pre-pregnancy weight og BMI |
| `symphysisFundalHeights` | Ja | Measurement, date og pregnancy week |
| `antenatalAppointments` | Ja | Encounter-datoer og eksplisitte measurements/findings |
| `pointsOfContact` | Ja | Ikke eksponert; fasaden er ikke en directory/demographics source |
| `birthStatus` | Ja | Ikke eksponert i første release for active pregnancy |

Hver resource DTO aksepterer ukjente JSON properties for forward compatibility. Eksakte property names er fortsatt case-sensitive, inkludert `bMI`. Resources merket `metadata.enteredInError=true` ekskluderes. Se [mappingmatrisen](mapping.md) for oppførsel på feltnivå.

Source-referanse: [NHN DHG resource-dokumentasjon](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/resourcesmd/). Valider den på nytt før en contract- eller terminology-oppgradering.
