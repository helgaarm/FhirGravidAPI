# DHG → FHIR mappingmatrise

For en resource-oriented oversikt over alle FHIR resources fasaden kan opprette, se [Mapping fra DHG API til FHIR R4 resources](dhg-fhir-resource-mapping.md).

Klassifisering:

- **DIRECT**: Et eksplisitt source field blir det samme clinical fact i FHIR.
- **PARTIAL**: Bare den semantisk sikre delen eksponeres; avgrensningen er angitt.
- **UNSUPPORTED**: Feltet beholdes i DTO for contract tolerance, men eksponeres ikke.

Alle resources med `metadata.enteredInError=true` filtreres. `null` betyr ukjent eller ikke registrert og gir ingen Observation. En eksplisitt `false` beholdes. Vanlige clinical facts bruker `valueBoolean=false`; DHG laboratory booleans bruker kodeverk 8340 `T008 |Negativ|` fordi source contract uttrykkelig definerer boolean som positivt/negativt prøvesvar.

| DHG-område/felt | FHIR mapping | Status | Regel |
|---|---|---|---|
| `metadata.recordId`, `recordStatus.status` | intern consistency check | DIRECT | record ID må samsvare med `/status`; status må være `ACTIVE` |
| `metadata.recordLastUpdated`, resource `metadata.lastUpdated` | `meta.lastUpdated` | DIRECT | source timestamp beholdes |
| `mother.language` | `Patient.communication.language` | DIRECT | bare dokumentert Volven 3303 code/system/display beholdes |
| `mother.needsLanguageInterpreter` | HL7 extension `patient-interpreterRequired` | DIRECT | nullable boolean beholdes |
| øvrige `mother`-felt | — | UNSUPPORTED | demography, employment og contact data er utenfor minimal Patient |
| `currentPregnancy.dateLastPeriod` | LOINC `8665-2`, `valueDateTime` med day precision | DIRECT | eksplisitt dato; ingen rekalkulering |
| `dueDate` | SNOMED CT `289206005` + LOINC `11778-8`, `valueDateTime` med day precision | DIRECT | method beholdes i SNOMED CT concept |
| `dueDateBasedOnUltrasound` | SNOMED CT `738070007` + LOINC `11778-8`, `valueDateTime` med day precision | DIRECT | method beholdes i SNOMED CT concept |
| `dueDateCorrectedDate` | — | UNSUPPORTED | clinical precedence og reason er ikke entydig dokumentert |
| `numberOfFetuses` | SNOMED CT `246435002`, `valueInteger` | DIRECT | eksplisitt antall |
| `assistedConception.hadAssistedConception`, `dateAssistedConception` | SNOMED CT `813541000000100`, `valueBoolean`, valgfri `effectiveDateTime` med day precision | DIRECT | FinnKode har norsk term «svangerskap ved assistert befruktning»; dato brukes bare når status eksplisitt er `true`, og status eller dato utledes aldri fra det andre feltet |
| `birthPreparationTalk` | SNOMED CT `702396006`, `valueBoolean` | DIRECT | eksplisitt childbirth education fact |
| `breastfeedingGuidance` | SNOMED CT `243094003`, `valueBoolean` | DIRECT | eksplisitt breastfeeding education fact |
| `hasPrenatalDiagnosticsTests` | — | UNSUPPORTED | DHG-feltet skiller ikke screening fra diagnostic procedure godt nok for en sikker code |
| `numberOfPreviousPregnancies` | SNOMED CT `246211005`, `valueInteger` | DIRECT | tidligere, ikke totalt antall pregnancies |
| `numberOfPreviousLiveBirths` | LOINC `11636-8`, `valueInteger` | DIRECT | total live births |
| `spontaneousMiscarriages` | SNOMED CT `248989003`, `valueInteger` | DIRECT | beholdes separat |
| `stillBirths22weeks` | SNOMED CT `252112002`, `valueInteger` | PARTIAL | DHG threshold står i source contract; ingen snevrere standard code er lagt til |
| `numberOfEctopicPregnancies` | SNOMED CT `440537001`, `valueInteger` | DIRECT | beholdes separat |
| provosert abort og `previousPregnancies.note` | — | UNSUPPORTED | ingen residual calculation og ingen free-text interpretation |
| `geneticDisorders.noneKnown` | text-only `Observation.code`, `valueBoolean` | DIRECT | literal source answer; `false` betyr bare at «ingen kjente» ikke ble bekreftet og brukes ikke til å utlede en diagnose |
| `geneticDisorders.parentsAreRelatives` | SNOMED CT `842009`, `valueBoolean` | DIRECT | consanguinity fact |
| `geneticDisorders.other` | text-only `Observation.code`, `valueBoolean` | DIRECT | uttrykker bare om annen arvelig sykdom er markert; ingen sykdomstype utledes |
| `geneticDisorders.note` | text-only `Observation.code`, `valueString` | DIRECT | trimmet source text beholdes ordrett og parses ikke til diagnose, person eller slektskap |
| `geneticDisorders.hipDysplasia` | — | UNSUPPORTED | ikke markert for extraction i gjeldende scope; subject/family-history semantics er ikke entydig nok |
| `medicalConditions.heartDisease` | SNOMED CT `56265001`, `valueBoolean` | DIRECT | broad DHG fact beholdes uten mer spesifikk diagnosis inference |
| `highBloodPressure` | SNOMED CT `38341003`, `valueBoolean` | DIRECT | ingen subtype inference |
| `diabetes` | SNOMED CT `73211009`, `valueBoolean` | PARTIAL | DHG skiller ikke diabetes fra gestational diabetes |
| `epilepsy`, `thrombosis`, `autoimmuneDisease`, `mentalHealth` | SNOMED CT `84757009`, `439127006`, `85828009`, `74732009` | DIRECT | nullable booleans beholdes |
| sammensatte/andre medical fields og `note` | — | UNSUPPORTED | blant annet `allergiesAsthma` og gynecological condition/procedure kan ikke splittes eller kodes sikkert |
| `drugAllergy` | SNOMED CT `416098002`, `valueBoolean` | DIRECT | eksplisitt fact |
| `folate.takenBefore`, `takenDuring` | SNOMED CT `792807003`, `valueBoolean` | PARTIAL | tidscontext beholdes som annotation; statusene utledes ikke fra hverandre |
| `medicationFrequency`, medication `note` | — | UNSUPPORTED | local enum/free text blir ikke en standard code eller `MedicationStatement` |
| `lifestyleFactors.stimuli[].stimuliType` | Volven 8536 som `Observation.code` | DIRECT | bare dokumentert national code system godtas |
| stimulus frequency | Volven 8537 som `valueCodeableConcept` | DIRECT | first consultation og week 36 blir separate Observations med annotation |
| stimulus `dailyCount` | — | UNSUPPORTED | ingen entydig generic national/standard code er dokumentert |
| `clinicalTests.hemoglobin`, `hemoglobinAt3rdTrimester` | NLK `NOR05172`, UCUM `g/dL` | DIRECT | samme analysis code; third trimester markeres med annotation; NILAR brukes som mapping reference |
| `ferritin`, `bHbA1c` | NLK `NPU19763`, `NPU27300` | DIRECT | units følger DHG/NLK contract |
| `hbv` | SNOMED CT `165806002`; kodeverk 8340 `T002`/`T008` result | DIRECT | DHG identifiserer uttrykkelig hepatitis B surface antigen; `true` betyr `Positiv`, `false` betyr `Negativ`, og `null` utelates |
| `hbvCore`, `bloodAntibodies`, `hiv`, `syphilis`, `chlamydia`, `toxoplasmosis`, `hepatitisC` | presis DHG-term i `Observation.code.text`; kodeverk 8340 `T002`/`T008` result | PARTIAL | public DHG contract definerer `true` som positivt prøvesvar, `false` som negativt og `null` som ikke tatt; ingen mer spesifikk analytt, antistoffidentitet eller facade code konstrueres |
| `rubellaAntigen` | NLK `NPU12412` P-Rubellavirus IgG; kodeverk 8340 `T002`/`T008` result | DIRECT | mappingen følger den autoritative DHG-beskrivelsen og NLK-koden, ikke det misvisende JSON-feltnavnet; `null` utelates |
| `asymptomaticBacteriuria`, `groupBStreptococci` | — | UNSUPPORTED | feltene må verifiseres mot autorisert gjeldende DHG contract før de kan publiseres |
| `aboRh.aboType`, `rhesusDType` | NLK `NPU58582`, `NPU21917` + LOINC `883-9`, `10331-7`; SNOMED CT coded value | DIRECT | norske laboratory codes er med; LOINC beholdes som interoperabel tilleggskoding; ukjente enum values eksponeres ikke |
| `glucoseTolerance.*Level` | SNOMED CT `271062006`, `49167009`; UCUM `mmol/L` | DIRECT | positiv value kreves; test date blir `effectiveDateTime` med day precision |
| `mrsaVreEsbl`, `gonorrhea`, `cytomegaloVirus`, clinical `note` | — | UNSUPPORTED | source identifiserer ikke en entydig assay/finding code |
| `rhesusDNegative.prophylaxisAtWeek28` | SNOMED CT `408783007`, `valueBoolean` | DIRECT | antenatal anti-D prophylaxis status |
| øvrige `rhesusDNegative`-felt | — | UNSUPPORTED | consent krever annen FHIR resource; fetal result mangler `fosterId` og kan derfor ikke bindes entydig til ett foster ved flerlinger |
| `vitalMeasurementsBeforePregnancy.height` | SNOMED CT `50373000` + LOINC `8302-2`, UCUM `cm`, `valueQuantity` | PARTIAL | standard FHIR R4 base Observation med `category=vital-signs`; manglende source measurement time betyr at `effective[x]` utelates og Vital Signs profile conformance ikke deklareres |
| `vitalMeasurementsBeforePregnancy.prePregnancyWeight` | SNOMED CT `27113001` + LOINC `29463-7`, UCUM `kg`, `valueQuantity` | PARTIAL | pre-pregnancy context beholdes i annotation; ingen `effective[x]` eller draft/profile claim konstrueres |
| `vitalMeasurementsBeforePregnancy.bMI` | SNOMED CT `60621009` + LOINC `39156-5`, UCUM `kg/m2`, `valueQuantity` | PARTIAL | positive source value beholdes som base R4 Observation; ingen measurement time eller profile conformance utledes |
| `symphysisFundalHeights[].measurement` | SNOMED CT `364253002`, UCUM `cm` | DIRECT | bare positiv value; measurement date blir `effectiveDateTime` med day precision |
| `antenatalAppointments[].appointmentDate` | `Encounter.period` | DIRECT | Encounter status forblir `unknown` |
| gestational week/day | LOINC `18185-9`, UCUM `d` | DIRECT | ett exact total-day Quantity per datert appointment; original `week+day` beholdes som annotation |
| mother weight | SNOMED CT `27113001` + LOINC `29463-7`, UCUM `kg` | DIRECT | norsk SNOMED CT coding og HL7 interoperability coding; refererer til Encounter når appointment date finnes |
| blood pressure `NNN/NN` | LOINC `85354-9`; components SNOMED CT `4471000202106`/`4481000202108` + LOINC `8480-6`/`8462-4` | PARTIAL | positive, sikkert parsbare components publiseres som standard FHIR R4 Observation uten draft canonical |
| protein in urine | NLK `NPU04206` med kodeverk 8340 `T008`/`T052`/`T048`/`T049`/`T050` | DIRECT | DHG enum `Neg`, `Spor`, `1+`, `2+`, `3+` oversettes eksplisitt; ukjente values utelates |
| edema | — | UNSUPPORTED | DHG definerer bare accepted integer `0..3`, ikke betydningen av hvert scale-trinn; rå integer publiseres ikke |
| `fetusesVitalSigns[].fosterId` | minimal pregnancy-scoped `Patient.id`; referert fra `Observation.focus` | DIRECT | bare positivt, eksplisitt `fosterId`; ID-en avledes deterministisk fra maternal logical ID og `fosterId`, inneholder ikke NIN, og brukes ikke som clinical identifier |
| `fetusesVitalSigns[].fetalHeartRate` | SNOMED CT `364075005` + LOINC `55283-6`, UCUM `{beats}/min`, `valueQuantity` | DIRECT | bare positiv source value fra datert appointment; mor er `subject`, foster-Patient er `focus` |
| `fetusesVitalSigns[].fetalPresentationLie` | text-only `Observation.code`; Volven 8534 i `valueCodeableConcept` | DIRECT | source code/system/display beholdes bare når code system er dokumentert Volven 8534; method utledes ikke |
| `fetusesVitalSigns[].motherFeelsBabyMovements` | LOINC `57088-7`, `valueBoolean` | DIRECT | eksplisitt maternal report; `false` beholdes og `null` utelates. SNOMED CT `268470003` brukes ikke som tilleggskode fordi den betyr en positiv finding og derfor ikke er state-neutral ved `false` |
| `fetusesVitalSigns[].note` | text-only `Observation.code`, `valueString` | DIRECT | trimmet source text beholdes ordrett og tolkes ikke til diagnosis eller finding |
| fetus entry uten positivt `fosterId`, eller uten datert appointment | — | UNSUPPORTED | det opprettes verken fetus Patient eller fetus Observation uten entydig pregnancy-scoped identity og temporal context |
| `pointsOfContact.midwife.name` | contained `Practitioner.name.text` i `CareTeam.participant.member` | DIRECT | source string trimmes; det konstrueres ingen directory-identitet |
| `pointsOfContact.midwife.organizationName` | contained `Organization.name` i `CareTeam.participant.onBehalfOf` | DIRECT | publiseres bare sammen med jordmor-deltakeren eller som organization participant når personnavn mangler |
| `pointsOfContact.maternityHealthcareCentre` | contained `Organization.name` i `CareTeam.participant.member` med text role `Helsestasjon` | DIRECT | free-text navn; det konstrueres ingen organization identifier eller managing responsibility |
| `pointsOfContact.generalPractitioner`, `birthInstitute`, `birthStatus`, `lastUpdatedBy` | — | UNSUPPORTED | ikke grønnmerket og utenfor gjeldende population/FHIR scope; ingen GP- eller directory lookup |

## Terminologiregler

- Fasaden publiserer ingen facade-specific `Observation.code` under `urn:nhn:population-data`.
- HL7 core extension brukes for interpreter requirement.
- NLK er det nasjonale laboratoriesystemet. Kodene er internasjonale NPU-koder eller norske NOR-koder; «NorLOINC» er ikke et eget code system.
- LOINC brukes som HL7 interoperability coding der mappingen er entydig. Når en entydig norsk SNOMED CT- eller NLK-kode finnes, publiseres den sammen med LOINC. UCUM brukes for machine-readable units.
- NLK og Volven brukes bare når DHG contract eller en autoritativ national source gir en entydig mapping.
- SNOMED CT brukes for eksakte clinical concepts som er verifisert som active i den norske terminology service. ICD-10 brukes ikke for broad booleans eller measurements; DHG-feltene er ikke tilstrekkelige til å etablere en konkret diagnosis.
- En Observation kan ha flere standard `Coding`-verdier når de uttrykker komplementær og sann semantics, for eksempel norsk SNOMED CT sammen med LOINC. Rekkefølgen i `CodeableConcept.coding` uttrykker ikke prioritet.
- Alle Observations bruker standard FHIR R4 `Observation` base resource. Fasaden publiserer ikke spesialiserte profiler i `meta.profile` eller `CapabilityStatement.supportedProfile`.
- NILAR `NilarObservation` brukes bare som mapping reference for laboratory code og UCUM. Fasaden deklarerer ikke NILAR-conformance fordi dagens DHG snapshot-mapping ikke leverer profilens mandatory `DiagnosticReport` reference og report-specific terminology.
- Ukjent code system, ny enum value eller free text oversettes aldri automatisk til en standard code. Et eksplisitt godkjent text field kan beholdes ordrett i en text-only Observation uten semantic parsing.
- Når DHG dokumenterer et entydig broad test result, men ingen entydig norsk eller internasjonal analyttkode er verifisert, kan `Observation.code` bruke source-preserving `CodeableConcept.text` uten `Coding`. Slike Observations kan ikke treffes med `code=system|code` før en standard coding er godkjent.
- Numeric measurements med en dokumentert DHG positivity constraint utelates når value er `0` eller negativ. Pre-pregnancy height, weight og BMI, `numberOfFetuses`, `fosterId`, fetal heart rate og pregnancy week må være positive, days after full week må være `0..6`, og blood pressure components må være positive. Dette er source-contract validation, ikke clinical reference-range inference.
- Terminology, code version og units må fortsatt godkjennes av clinical terminology owner før DHG Test/Production.

Autoritative referanser: [DHG Resources](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/resourcesmd/), [Norsk laboratoriekodeverk (NLK)](https://www.helsedirektoratet.no/digitalisering-og-e-helse/helsefaglige-kodeverk/nlk), [veileder for NLK](https://www.helsedirektoratet.no/veiledere/veileder-for-norsk-laboratoriekodeverk-nlk), [NILAR/Pasientens Prøvesvar](https://github.com/HL7Norway/NILAR), [NilarObservation](https://hl7norway.github.io/NILAR/DiagnosticReportIG/CurrentBuild/StructureDefinition-nilar-observation.html), [HL7 FHIR R4 Observation og `focus`](https://hl7.org/fhir/R4/observation.html), [FHIR R4 fetal Patient example](https://hl7.org/fhir/R4/patient-examples.html), [LOINC 55283-6](https://loinc.org/55283-6), [LOINC prenatal assessment panel](https://loinc.org/100230-2), [Helsedirektoratet om SNOMED CT](https://www.helsedirektoratet.no/digitalisering-og-e-helse/snomed-ct) og [FinnKode](https://finnkode.helsedirektoratet.no/).
