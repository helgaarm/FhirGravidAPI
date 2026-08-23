# DHG-kontrakt for population coverage

Fasaden eksponerer `Patient`, `Observation`, `Encounter` og et avgrenset `CareTeam`. Den implementerer ikke `$populate`, Questionnaire processing, demographics lookup, GP lookup, Grunndata eller andre clinical data sources.

## Consumer contract

- `Patient/{id}` returnerer enten den minimale mor-Patient eller en pregnancy-scoped fetus Patient. Ingen av dem inneholder NIN. Fetus Patient inneholder bare logical `id` og valgfri `meta.lastUpdated`; navn, gender, birthDate og identifier utledes ikke.
- `CareTeam` eksponerer bare DHG `pointsOfContact.midwife` og `maternityHealthcareCentre`. Person og organization er contained resources fordi fasaden ikke gjør directory lookup; HPR-, organization- eller andre identifiers konstrueres ikke.
- GET Observation search krever `patient={logical-id}` og aksepterer valgfritt `code`, `category` og day-precision `date`. POST `_search` bruker `patient.identifier` i form body, støtter de samme filtrene og krever HelseID utenfor lokal `DevelopmentTestMode`.
- En manglende eller `null` DHG-verdi produserer ingen Observation. Eksplisitt `false` beholdes; DHG laboratory results bruker kodeverk 8340 `T008 |Negativ|`, mens andre booleans bruker `valueBoolean=false`.
- `currentPregnancy.hasPrenatalDiagnosticsTests` eksponeres source-preserving som «Gitt informasjon om fosterdiagnostikk». `true` og `false` beholdes, men verdien uttrykker ikke om en undersøkelse er utført, et prøveresultat eller et samtykke.
- De markerte genetic-disorder-feltene `noneKnown`, `parentsAreRelatives`, `other` og `note` eksponeres. Broad booleans bruker source-faithful value, og note beholdes som trimmet `valueString`; fasaden utleder ikke diagnose, berørt person eller slektskap fra teksten.
- `hipDysplasia` eksponeres som et source-preserving familiehistorisk boolean-svar. Berørt person og klinisk diagnose utledes ikke.
- `previousPregnancies.note` eksponeres som trimmet, uparset `valueString`; det gjøres ingen residualberegning eller inference av svangerskapsutfall eller prosedyre.
- Korrigert termindato eksponeres som en separat text-only datofact. Den overskriver ikke andre termindatoer, og fasaden velger ikke clinical precedence eller utleder korreksjonsgrunn.
- Samboerskap med medforelder og tilhørende note eksponeres som source-preserving social history. Appointment medication-svar og note er encounter-scoped; relasjon, husstand, legemiddel, dose, diagnose og vurdering utledes ikke.
- Medication frequency/note og clinical-tests note eksponeres som trimmet, uparset `valueString`. De blir ikke tolket som legemiddel, dose, instruksjon, analytt, prøveresultat eller clinical assessment.
- Broad testfelt for MRSA/VRE/ESBL, gonoré, cytomegalovirus, asymptomatisk bakteriuri og gruppe B-streptokokker eksponeres med text-only test concept og eksplisitt positivt/negativt kodeverk 8340-resultat; ingen assay- eller analyttkode konstrueres.
- Ikke-negativ lifestyle `dailyCount` beholdes som en integer component på den aktuelle coded stimulus/frequency Observation uten konstruert unit. Edema beholdes som raw encounter-scoped integer `0..3` uten tolkning av scale-trinnene.
- Sammensatte/andre `medicalConditions`-booleans eksponeres med presis DHG-term og `valueBoolean`; `null` utelates. De splittes ikke til separate diagnoser eller prosedyrer, og hver Observation forklarer begrensningen i `Observation.note`. Medical note beholdes som trimmet `valueString` uten semantic parsing.
- `metadata.enteredInError=true` produserer ingen FHIR resource.
- `meta.lastUpdated` kommer fra DHG source metadata når de er tilgjengelige.
- Gestational age bruker LOINC `18185-9` og ett UCUM-day Quantity per datert appointment. Fasaden oppretter ikke en ekstra facade-specific «latest» Observation; consumer kan velge nyeste `effectiveDateTime`.
- Et positivt `fetusesVitalSigns[].fosterId` oppretter en separat fetus Patient. Fetal heart rate, presentation/lie, maternal report of movements og uparset fetus note blir Observations med mor som `subject`, fosteret som `focus` og antenatal appointment som `encounter`.
- Pre-pregnancy height, weight og BMI eksponeres som standard FHIR R4 base Observations med SNOMED CT/LOINC og UCUM. DHG leverer ingen measurement time, så `effective[x]` utelates; `meta.lastUpdated` må ikke tolkes som measurement time, og Vital Signs profile conformance deklareres ikke.
- Etter vellykket patient selection returnerer search uten clinical treff en FHIR `searchset` Bundle med `total=0`.

## Publiserte terminology systems

Fasaden publiserer ikke egne clinical codes under `urn:nhn:population-data`. `Observation.code` og coded values bruker mappings som er verifisert mot en autoritativ source. Når DHG dokumenterer et entydig broad test result uten en verifisert standard analyttkode, brukes den presise source-termen i `CodeableConcept.text` uten `Coding`:

| System | Canonical URI i FHIR | Bruk |
|---|---|---|
| LOINC | `http://loinc.org` | HL7-recommended interoperability coding; norsk tilleggskode publiseres når den er entydig |
| SNOMED CT | `http://snomed.info/sct` | clinical findings, observable entities og procedures med exact semantic match |
| NLK | `urn:oid:2.16.578.1.12.4.1.1.7280` | nasjonalt laboratoriesystem med NPU- og NOR-koder dokumentert av DHG/Helsedirektoratet |
| Volven | relevant `urn:oid:2.16.578.1.12.4.1.1.*` | national value sets for language, lifestyle og urine/laboratory result |
| UCUM | `http://unitsofmeasure.org` | machine-readable quantity units |

`Patient.needsLanguageInterpreter` bruker HL7 extension `http://hl7.org/fhir/StructureDefinition/patient-interpreterRequired`.

Noen facts har flere standard `Coding`-verdier:

- due date from last period: SNOMED CT `289206005` og LOINC `11778-8`
- due date from antenatal ultrasound: SNOMED CT `738070007` og LOINC `11778-8`
- body weight fra datert appointment: SNOMED CT `27113001` og LOINC `29463-7`

Det finnes ikke et eget «NorLOINC»-system. Norske laboratory concepts publiseres fra NLK. `CodeableConcept.coding` har ingen prioritetsrekkefølge; alle codings skal være sanne samtidig.

`code` search matcher alle publiserte `Observation.code.coding` entries. Text-only test concepts returneres uten `code`-filter eller med `category=laboratory`, men kan ikke treffes med `code=system|code` før en standard coding er godkjent.

## Viktige query concepts

| Fact | `system|code` |
|---|---|
| last menstrual period | `http://loinc.org|8665-2` |
| gestational age | `http://loinc.org|18185-9` |
| body weight | `http://snomed.info/sct|27113001` eller `http://loinc.org|29463-7` |
| body height | `http://snomed.info/sct|50373000` eller `http://loinc.org|8302-2` |
| body mass index | `http://snomed.info/sct|60621009` eller `http://loinc.org|39156-5` |
| blood pressure panel | `http://loinc.org|85354-9` |
| estimated delivery date | `http://loinc.org|11778-8` |
| pregnancy resulting from assisted conception | `http://snomed.info/sct|813541000000100` |
| fetal heart rate | `http://snomed.info/sct|364075005` eller `http://loinc.org|55283-6` |
| fetal movements reported | `http://loinc.org|57088-7` |

Blood pressure components bruker norske SNOMED CT-koder `4471000202106` og `4481000202108` sammen med LOINC `8480-6` og `8462-4`. Panelkoden forblir LOINC `85354-9` fordi det ikke er verifisert en entydig norsk SNOMED CT panelkode.

## Eksplisitt unsupported eller partial

- Medication name/dose, diagnosis og andre clinical facts trekkes ikke ut fra free text.
- Combined DHG fields som `allergiesAsthma` og `mrsaVreEsbl` splittes ikke og får ingen misvisende standard code.
- Consent eksponeres ikke som Observation. Fetal RhD result holdes tilbake fordi source-blokken mangler `fosterId` og derfor ikke kan bindes entydig til ett foster ved flerlinger.
- Stimulus `dailyCount` eksponeres ikke før en semantic standard mapping er godkjent.
- Øvrige contact/demographic data, inkludert GP og birth institute, samt birth-status er utenfor gjeldende API surface.
- Ukjente source fields, code systems og enum values tolereres i DTO, men eksponeres ikke automatisk.
- Blood pressure eksponeres bare når dokumentert `systolic/diastolic` format kan parses sikkert.
- Numeric values med DHG positivity constraint utelates når de er `0` eller negative. Dette innfører ingen clinical reference ranges.
- Edema grade eksponeres ikke før scale semantics er godkjent. Fetus-spesifikke appointment facts eksponeres bare når både appointment date og et positivt `fosterId` finnes.

Full field classification finnes i [mapping.md](mapping.md). Query-eksempler finnes i [examples/fhir-queries.md](../examples/fhir-queries.md). Terminology og units krever fortsatt godkjenning fra clinical terminology owner før DHG Test/Production.
