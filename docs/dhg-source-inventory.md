# DHG source inventory

Denne oversikten beskriver DHG read model som fasaden bruker. DHG er fortsatt eneste runtime source. API-et leser først `/status`, krever eksplisitt consent og en active record, leser deretter `/record/{latestRecordId}` og verifiserer samsvarende identitet og `ACTIVE` status.

| DHG resource area | DTO support | Eksponert i første FHIR surface |
|---|---|---|
| `mother` | Ja | Preferred language, behov for tolk og source-preserving samboerskap med medforelder/note; øvrig demography og employment utelates |
| `currentPregnancy` | Ja | Eksplisitte dates, inkludert separat korrigert termindato uten precedence inference, fetal count, assisted-conception status/date og counselling flags, inkludert source-preserving «Gitt informasjon om fosterdiagnostikk»; det utledes ingen gjennomført test eller testresultat |
| `previousPregnancies` | Ja | Eksplisitte counters og source-preserving uparset note; ingen residualberegning |
| `geneticDisorders` | Ja | Eksplisitte nullable booleans, inkludert source-preserving hofteleddsdysplasi-familiehistorikk, og uparset note |
| `medicalConditions` | Ja | Eksplisitte nullable booleans og uparset note; sammensatte source fields beholdes som text-only concepts med field-specific limitations i `Observation.note` |
| `medication` | Ja | Uparset frequency/note samt allergy- og folate-fakta; ingen infererte legemiddelnavn, doser eller instrukser |
| `lifestyleFactors` | Ja | Eksplisitte coded stimuli/frequencies og source-preserving daily-count components uten konstruert unit |
| `clinicalTests` | Ja | Eksplisitte resultater med konservativ NLK/LOINC/SNOMED CT terminology eller presise text-only concepts; uparset note |
| `rhesusDNegative` | Ja | Consent, resultat, resultatdato og prophylaxis |
| `vitalMeasurementsBeforePregnancy` | Ja | Positive height (`cm`), pre-pregnancy weight (`kg`) og BMI eksponeres som base R4 Observations uten konstruert measurement time eller Vital Signs profile claim |
| `symphysisFundalHeights` | Ja | Measurement, date og pregnancy week |
| `antenatalAppointments` | Ja | Encounter-datoer, eksplisitte maternal measurements/findings, source-preserving medication-svar/note og fetus Patients/Observations for identifiserte `fetusesVitalSigns` |
| `pointsOfContact` | Ja | Fastlege, jordmor og maternity healthcare centre eksponeres konservativt i `CareTeam`; source-provided HPR number og fastlegens organisasjonsnummer beholdes som FHIR identifiers, birth institute utelates, og det gjøres ingen ekstern GP- eller directory lookup |
| `birthStatus` | Ja | Ikke eksponert i første release for active pregnancy |

Hver resource DTO aksepterer ukjente JSON properties for forward compatibility. Eksakte property names er fortsatt case-sensitive, inkludert `bMI`. Resources merket `metadata.enteredInError=true` ekskluderes. Se [mappingmatrisen](mapping.md) for oppførsel på feltnivå.

Source-referanse: [NHN DHG resource-dokumentasjon](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/resourcesmd/). Valider den på nytt før en contract- eller terminology-oppgradering.
