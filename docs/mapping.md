# DHG → FHIR mappingmatrise

Klassifisering:

- **DIRECT**: eksplisitt kildefelt blir samme kliniske fakta i FHIR.
- **PARTIAL**: bare den semantisk sikre delen eksponeres; avgrensningen er angitt.
- **UNSUPPORTED**: lagres i DTO for kontraktstoleranse, men eksponeres ikke.

Alle resources med `metadata.enteredInError = true` filtreres. `null` betyr ukjent/ikke registrert og gir ingen Observation; `false` er en eksplisitt verdi og beholdes.

| DHG-område/felt | FHIR | Status | Regel |
|---|---|---|---|
| `metadata.recordId`, `recordStatus.status` | intern konsistenskontroll | DIRECT | record-ID må matche `/status`; status må være `ACTIVE` |
| `metadata.recordLastUpdated`, resource `metadata.lastUpdated` | `meta.lastUpdated` | DIRECT | kilde-tid beholdes |
| `mother.language` | `Patient.communication.language` | DIRECT | kode/system/display beholdes |
| `mother.needsLanguageInterpreter` | Patient extension | DIRECT | nullable boolean beholdes |
| øvrige `mother`-felt | — | UNSUPPORTED | navn, adresse, arbeid, fødeland og samliv er utenfor minimal Patient |
| `currentPregnancy.dateLastPeriod` | Observation `valueDate` | DIRECT | eksplisitt dato |
| `dueDate` | termin fra siste menstruasjon, `valueDate` | DIRECT | ingen rekalkulering |
| `dueDateBasedOnUltrasound` | ultralydtermin, `valueDate` | DIRECT | ingen rekalkulering |
| `dueDateCorrectedDate` | — | UNSUPPORTED | kildefeltets kliniske prioritet/årsak er ikke entydig dokumentert i fasadekontrakten |
| `numberOfFetuses` | Observation `valueInteger` | DIRECT | eksplisitt antall |
| `assistedConception.*` | boolean/date Observations | DIRECT | ingen avledning |
| `hasPrenatalDiagnosticsTests`, `birthPreparationTalk`, `breastfeedingGuidance` | boolean Observations | DIRECT | eksplisitte fakta |
| alle tellere i `previousPregnancies` | integer Observations | DIRECT | hver teller holdes separat |
| provosert abort | — | UNSUPPORTED | finnes ikke eksplisitt og beregnes aldri som restkategori |
| `previousPregnancies.note` | tekst-Observation | DIRECT | merkes som fritekst, ikke tolket |
| `geneticDisorders` boolean-felt | boolean Observations | DIRECT | separate eksplisitte fakta |
| `geneticDisorders.note` | tekst-Observation | DIRECT | ikke tolket |
| alle boolean-felt i `medicalConditions` | boolean Observations | PARTIAL | `allergiesAsthma` forblir ett sammensatt fakta; splittes ikke |
| `medicalConditions.note` | tekst-Observation | DIRECT | ikke til diagnosekode |
| `medication.medicationFrequency` | kodet Observation | PARTIAL | kildeverdien beholdes; fritekstnote kan være annotation |
| `drugAllergy`, `folate.*` | boolean Observations | DIRECT | nullable boolean beholdes |
| legemiddelnavn/dose fra `medication.note` | — | UNSUPPORTED | ingen `MedicationStatement` opprettes fra fritekst |
| `lifestyleFactors.stimuli[].stimuliType` | social-history Observation | DIRECT | kildekode/system beholdes |
| stimulusfrekvens/daglig antall | Observation components | DIRECT | første konsultasjon og uke 36 holdes separate |
| lifestyle `note` | Observation annotation | PARTIAL | ikke tolket |
| `clinicalTests.hemoglobin`, `hemoglobinAt3rdTrimester` | quantity Observations | DIRECT | separate facade-koder for trimester; UCUM `g/dL`, med NOR05172 som enhetskilde |
| `clinicalTests.ferritin`, `bHbA1c` | quantity Observations | DIRECT | eksplisitt verdi og dokumentert enhet |
| `clinicalTests` infeksjons-/screeningbooleans | boolean Observations | DIRECT | HBV, HBV core og toxoplasmose bruker facade-koder fordi DHG-feltet ikke angir én entydig NLK-analyse; øvrige sikre NLK-koblinger beholdes |
| `aboRh.*` | coded Observations | DIRECT | ingen terminologisk gjetning |
| `glucoseTolerance.*Level`, `testDate` | quantity Observations + `effectiveDate` | DIRECT | fastende og 2-timersverdi holdes separate |
| `clinicalTests.note` | — | UNSUPPORTED | uspesifikk note knyttes ikke til enkeltprøver |
| `rhesusDNegative` boolean-felt | boolean Observations | DIRECT | samtykke, fosterresultat og profylakse holdes separate |
| `dateForResult` | egen date Observation + RhD-resultatets `effectiveDate` | DIRECT | separat søkbart fakta og tidskontekst for foster-RhD-resultatet |
| `rhesusDNegative.note` | — | UNSUPPORTED | ikke tolket |
| `vitalMeasurementsBeforePregnancy.height` | quantity cm | DIRECT | UCUM `cm` |
| `prePregnancyWeight` | quantity kg | DIRECT | UCUM `kg` |
| `bMI` | decimal Observation | DIRECT | wire-navnet er eksakt `bMI` |
| `symphysisFundalHeights[].measurement` | quantity cm | DIRECT | dato til `effectiveDate`, uke til component |
| `antenatalAppointments[].appointmentDate` | `Encounter.period` | DIRECT | besøksdato; status er `unknown` fordi DHG ikke oppgir gjennomføringsstatus |
| gestasjonsuke/dager | Observation + integer components | DIRECT | historikk bruker `gestational-age-at-appointment`; kun siste relevante avtale gir `recorded-gestational-age` |
| mors vekt, ødem | quantity/integer Observations | DIRECT | refererer til Encounter |
| blodtrykk `NNN/NN` | Observation med systolisk/diastolisk components | PARTIAL | kun dokumentert parsbar form eksponeres; ellers utelates |
| protein i urin | coded Observation | DIRECT | kildeverdi beholdes |
| fosterlyd, presentasjon/leie, mor kjenner liv | Observations | DIRECT | ett sett per eksplisitt foster-ID |
| appointment `medication`, `employmentRate`, `note`, fetus `note` | — | UNSUPPORTED | utilstrekkelig spesifikke for sikker klinisk mapping |
| `pointsOfContact` | — | UNSUPPORTED | fasaden er populasjonsdata, ikke katalog-/kontaktflate |
| `birthStatus` | — | UNSUPPORTED | første versjon er avgrenset til aktivt svangerskap; post-birth-modell krever egen beslutning |
| `lastUpdatedBy` | — | UNSUPPORTED | provenance-person-/organisasjonsdetaljer eksponeres ikke |

## Terminologi

Løsningen bruker dokumenterte NLK/Volven-identifikatorer bare når feltet har en entydig kobling. Fasadespesifikke eller flertydige konsepter ligger under `urn:nhn:population-data`; lokale strenger legges aldri i NLK-navnerommet. Nye eller ukjente kildekoder tolereres og beholdes; de oversettes ikke til en kode med annen klinisk betydning. Alle koder og enheter må godkjennes av klinisk terminologieier før DHG Test/produksjon.
