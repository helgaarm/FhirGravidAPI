# DHG → FHIR mappingmatrise

For en resource-oriented oversikt over alle FHIR resources fasaden kan opprette, se [Mapping fra DHG API til FHIR R4 resources](dhg-fhir-resource-mapping.md).

Klassifisering:

- **DIRECT**: Et eksplisitt source field blir det samme clinical fact i FHIR.
- **PARTIAL**: Bare den semantisk sikre delen eksponeres; avgrensningen er angitt.
- **UNSUPPORTED**: Feltet beholdes i DTO for contract tolerance, men eksponeres ikke.

Alle resources med `metadata.enteredInError=true` filtreres. `null` betyr ukjent eller ikke registrert og gir ingen Observation. En eksplisitt `false` beholdes. Vanlige clinical facts bruker `valueBoolean=false`; DHG laboratory booleans bruker kodeverk 8340 `T008 |Negativ|` fordi source contract uttrykkelig definerer boolean som positivt/negativt prøvesvar.

| DHG-område/felt | Status | FHIR mapping og regel |
|---|---|---|
| `metadata.recordId`, `recordStatus.status` | DIRECT | intern consistency check<br>**Regel:** record ID må samsvare med `/status`; status må være `ACTIVE` |
| `metadata.recordLastUpdated`, resource `metadata.lastUpdated` | DIRECT | `meta.lastUpdated`<br>**Regel:** source timestamp beholdes |
| `mother.language` | DIRECT | `Patient.communication.language`<br>**Regel:** bare dokumentert Volven 3303 code/system/display beholdes |
| `mother.needsLanguageInterpreter` | DIRECT | HL7 extension `patient-interpreterRequired`<br>**Regel:** nullable boolean beholdes |
| `mother.cohabitingCoparent` | DIRECT | text-only `Observation.code`, `valueBoolean`<br>**Regel:** eksplisitt social-history-svar; `false` beholdes og relasjon, foreldreansvar eller husstand utledes ikke |
| `mother.cohabitingCoparentNote` | PARTIAL | text-only `Observation.code`, uparset `valueString`<br>**Regel:** source text beholdes uten tolkning som relasjon, adresse eller sosialfaglig vurdering |
| øvrige `mother`-felt | UNSUPPORTED | —<br>**Regel:** demography, employment og contact data er utenfor minimal Patient/finding surface |
| `currentPregnancy.dateLastPeriod` | DIRECT | LOINC `8665-2`, `valueDateTime` med day precision<br>**Regel:** eksplisitt dato; ingen rekalkulering |
| `dueDate` | DIRECT | SNOMED CT `289206005` + LOINC `11778-8`, `valueDateTime` med day precision<br>**Regel:** method beholdes i SNOMED CT concept |
| `dueDateBasedOnUltrasound` | DIRECT | SNOMED CT `738070007` + LOINC `11778-8`, `valueDateTime` med day precision<br>**Regel:** method beholdes i SNOMED CT concept |
| `dueDateCorrectedDate` | PARTIAL | text-only `Observation.code`, `valueDateTime` med day precision<br>**Regel:** den eksplisitte korrigerte datoen beholdes som en separat source fact; fasaden velger ikke hvilken termindato som er klinisk gjeldende og utleder ikke korreksjonsgrunn |
| `numberOfFetuses` | DIRECT | SNOMED CT `246435002`, `valueInteger`<br>**Regel:** eksplisitt antall |
| `assistedConception.hadAssistedConception`, `dateAssistedConception` | DIRECT | SNOMED CT `813541000000100`, `valueBoolean`, valgfri `effectiveDateTime` med day precision<br>**Regel:** FinnKode har norsk term «svangerskap ved assistert befruktning»; dato brukes bare når status eksplisitt er `true`, og status eller dato utledes aldri fra det andre feltet |
| `birthPreparationTalk` | DIRECT | SNOMED CT `702396006`, `valueBoolean`<br>**Regel:** eksplisitt childbirth education fact |
| `breastfeedingGuidance` | DIRECT | SNOMED CT `243094003`, `valueBoolean`<br>**Regel:** eksplisitt breastfeeding education fact |
| `hasPrenatalDiagnosticsTests` | DIRECT | text-only `Observation.code`, `valueBoolean`<br>**Regel:** følger DHG-beskrivelsen «Gitt informasjon om fosterdiagnostikk»: `true`/`false` betyr at informasjon er/ikke er gitt, og `null` utelates; det utledes ikke test, resultat eller samtykke |
| `numberOfPreviousPregnancies` | DIRECT | SNOMED CT `246211005`, `valueInteger`<br>**Regel:** tidligere, ikke totalt antall pregnancies |
| `numberOfPreviousLiveBirths` | DIRECT | LOINC `11636-8`, `valueInteger`<br>**Regel:** total live births |
| `spontaneousMiscarriages` | DIRECT | SNOMED CT `248989003`, `valueInteger`<br>**Regel:** beholdes separat |
| `stillBirths22weeks` | PARTIAL | SNOMED CT `252112002`, `valueInteger`<br>**Regel:** DHG threshold står i source contract; ingen snevrere standard code er lagt til |
| `numberOfEctopicPregnancies` | DIRECT | SNOMED CT `440537001`, `valueInteger`<br>**Regel:** beholdes separat |
| provosert abort | UNSUPPORTED | —<br>**Regel:** det finnes ikke et eksplisitt source-felt, og fasaden gjør ingen residual calculation |
| `previousPregnancies.note` | PARTIAL | text-only `Observation.code`, uparset `valueString`<br>**Regel:** source text beholdes uten tolkning som svangerskapsutfall, diagnose, prosedyre eller beregningsgrunnlag |
| `geneticDisorders.noneKnown` | DIRECT | text-only `Observation.code`, `valueBoolean`<br>**Regel:** literal source answer; `false` betyr bare at «ingen kjente» ikke ble bekreftet og brukes ikke til å utlede en diagnose |
| `geneticDisorders.parentsAreRelatives` | DIRECT | SNOMED CT `842009`, `valueBoolean`<br>**Regel:** consanguinity fact |
| `geneticDisorders.other` | DIRECT | text-only `Observation.code`, `valueBoolean`<br>**Regel:** uttrykker bare om annen arvelig sykdom er markert; ingen sykdomstype utledes |
| `geneticDisorders.note` | DIRECT | text-only `Observation.code`, `valueString`<br>**Regel:** trimmet source text beholdes ordrett og parses ikke til diagnose, person eller slektskap |
| `geneticDisorders.hipDysplasia` | PARTIAL | text-only `Observation.code`, `valueBoolean`<br>**Regel:** source-svaret beholdes som familiehistorisk svar; berørt person og klinisk diagnose utledes ikke |
| `medicalConditions.heartDisease` | DIRECT | SNOMED CT `56265001`, `valueBoolean`<br>**Regel:** broad DHG fact beholdes uten mer spesifikk diagnosis inference |
| `highBloodPressure` | DIRECT | SNOMED CT `38341003`, `valueBoolean`<br>**Regel:** ingen subtype inference |
| `diabetes` | PARTIAL | SNOMED CT `73211009`, `valueBoolean`<br>**Regel:** DHG skiller ikke diabetes fra gestational diabetes |
| `epilepsy`, `thrombosis`, `autoimmuneDisease`, `mentalHealth` | DIRECT | SNOMED CT `84757009`, `439127006`, `85828009`, `74732009`<br>**Regel:** nullable booleans beholdes |
| `nothingParticular` | DIRECT | text-only `Observation.code`, `valueBoolean`<br>**Regel:** source answer beholdes; `false` betyr ikke at en sykdom er identifisert; begrensningen følger i `Observation.note` |
| `kidneyUrinaryTractDiseases`, `allergiesAsthma`, `gynecologicalConditions` | DIRECT | presis sammensatt DHG-term i `Observation.code.text`, `valueBoolean`<br>**Regel:** feltene splittes ikke til separate sykdommer, inngrep eller operasjoner; den feltspesifikke begrensningen følger i `Observation.note` |
| `medicalConditions.other` | DIRECT | text-only `Observation.code`, `valueBoolean`<br>**Regel:** uttrykker bare om annen medical condition er markert; ingen diagnose utledes |
| `medicalConditions.note` | DIRECT | text-only `Observation.code`, `valueString`<br>**Regel:** trimmet source text beholdes ordrett; `Observation.note` sier eksplisitt at teksten ikke tolkes som diagnose, legemiddel, prosedyre eller berørt person |
| `drugAllergy` | DIRECT | SNOMED CT `416098002`, `valueBoolean`<br>**Regel:** eksplisitt fact |
| `folate.takenBefore`, `takenDuring` | PARTIAL | SNOMED CT `792807003`, `valueBoolean`<br>**Regel:** tidscontext beholdes som annotation; statusene utledes ikke fra hverandre |
| `medicationFrequency`, medication `note` | PARTIAL | text-only `Observation.code`, uparset `valueString`<br>**Regel:** raw source-verdier beholdes; legemiddel, dose, indikasjon, instruksjon og standardisert frekvens utledes ikke |
| `lifestyleFactors.stimuli[].stimuliType` | DIRECT | Volven 8536 som `Observation.code`<br>**Regel:** bare dokumentert national code system godtas |
| stimulus frequency | DIRECT | Volven 8537 som `valueCodeableConcept`<br>**Regel:** first consultation og week 36 blir separate Observations med annotation |
| stimulus `dailyCount` | PARTIAL | text-only component code, `valueInteger`<br>**Regel:** ikke-negativ raw count beholdes som component på den aktuelle coded stimulus/frequency Observation; unit eller clinical interpretation utledes ikke |
| `clinicalTests.hemoglobin`, `hemoglobinAt3rdTrimester` | DIRECT | NLK `NOR05172`, UCUM `g/dL`<br>**Regel:** samme analysis code; third trimester markeres med annotation; NILAR brukes som mapping reference |
| `ferritin`, `bHbA1c` | DIRECT | NLK `NPU19763`, `NPU27300`<br>**Regel:** units følger DHG/NLK contract |
| `hbv` | DIRECT | SNOMED CT `165806002`; kodeverk 8340 `T002`/`T008` result<br>**Regel:** DHG identifiserer uttrykkelig hepatitis B surface antigen; `true` betyr `Positiv`, `false` betyr `Negativ`, og `null` utelates |
| `hbvCore`, `bloodAntibodies`, `hiv`, `syphilis`, `chlamydia`, `toxoplasmosis`, `hepatitisC` | PARTIAL | presis DHG-term i `Observation.code.text`; kodeverk 8340 `T002`/`T008` result<br>**Regel:** public DHG contract definerer `true` som positivt prøvesvar, `false` som negativt og `null` som ikke tatt; ingen mer spesifikk analytt, antistoffidentitet eller facade code konstrueres |
| `rubellaAntigen` | DIRECT | NLK `NPU12412` P-Rubellavirus IgG; kodeverk 8340 `T002`/`T008` result<br>**Regel:** mappingen følger den autoritative DHG-beskrivelsen og NLK-koden, ikke det misvisende JSON-feltnavnet; `null` utelates |
| `asymptomaticBacteriuria`, `groupBStreptococci` | PARTIAL | presis DHG-term i `Observation.code.text`; kodeverk 8340 `T002`/`T008` result<br>**Regel:** nullable source-resultat beholdes uten å konstruere analytt- eller assay-code |
| `aboRh.aboType`, `rhesusDType` | DIRECT | NLK `NPU58582`, `NPU21917` + LOINC `883-9`, `10331-7`; SNOMED CT coded value<br>**Regel:** norske laboratory codes er med; LOINC beholdes som interoperabel tilleggskoding; ukjente enum values eksponeres ikke |
| `glucoseTolerance.*Level` | DIRECT | SNOMED CT `271062006`, `49167009`; UCUM `mmol/L`<br>**Regel:** positiv value kreves; test date blir `effectiveDateTime` med day precision |
| `mrsaVreEsbl`, `gonorrhea`, `cytomegaloVirus` | PARTIAL | presis DHG-term i `Observation.code.text`; kodeverk 8340 `T002`/`T008` result<br>**Regel:** broad/composite source-resultat beholdes uten å konstruere analytt-, organisme- eller assay-code |
| clinical `note` | PARTIAL | text-only `Observation.code`, uparset `valueString`<br>**Regel:** source text beholdes uten tolkning som analytt, resultat, diagnose eller vurdering |
| `rhesusDNegative.prophylaxisAtWeek28` | DIRECT | SNOMED CT `408783007`, `valueBoolean`<br>**Regel:** antenatal anti-D prophylaxis status |
| øvrige `rhesusDNegative`-felt | UNSUPPORTED | —<br>**Regel:** consent krever annen FHIR resource; fetal result mangler `fosterId` og kan derfor ikke bindes entydig til ett foster ved flerlinger |
| `vitalMeasurementsBeforePregnancy.height` | PARTIAL | SNOMED CT `50373000` + LOINC `8302-2`, UCUM `cm`, `valueQuantity`<br>**Regel:** standard FHIR R4 base Observation med `category=vital-signs`; manglende source measurement time betyr at `effective[x]` utelates og Vital Signs profile conformance ikke deklareres |
| `vitalMeasurementsBeforePregnancy.prePregnancyWeight` | PARTIAL | SNOMED CT `27113001` + LOINC `29463-7`, UCUM `kg`, `valueQuantity`<br>**Regel:** pre-pregnancy context beholdes i annotation; ingen `effective[x]` eller draft/profile claim konstrueres |
| `vitalMeasurementsBeforePregnancy.bMI` | PARTIAL | SNOMED CT `60621009` + LOINC `39156-5`, UCUM `kg/m2`, `valueQuantity`<br>**Regel:** positive source value beholdes som base R4 Observation; ingen measurement time eller profile conformance utledes |
| `symphysisFundalHeights[].measurement` | DIRECT | SNOMED CT `364253002`, UCUM `cm`<br>**Regel:** bare positiv value; measurement date blir `effectiveDateTime` med day precision |
| `antenatalAppointments[].appointmentDate` | DIRECT | `Encounter.period`<br>**Regel:** Encounter status forblir `unknown` |
| appointment `medication` | PARTIAL | text-only `Observation.code`, `valueBoolean`<br>**Regel:** encounter-scoped source-svar beholdes; legemiddel, dose, indikasjon og behandlingsstatus utledes ikke |
| appointment `note` | PARTIAL | text-only `Observation.code`, uparset `valueString`<br>**Regel:** encounter-scoped source text beholdes uten tolkning som diagnose, legemiddel, prosedyre, måling eller vurdering |
| gestational week/day | DIRECT | LOINC `18185-9`, UCUM `d`<br>**Regel:** ett exact total-day Quantity per datert appointment; original `week+day` beholdes som annotation |
| mother weight | DIRECT | SNOMED CT `27113001` + LOINC `29463-7`, UCUM `kg`<br>**Regel:** norsk SNOMED CT coding og HL7 interoperability coding; refererer til Encounter når appointment date finnes |
| blood pressure `NNN/NN` | PARTIAL | LOINC `85354-9`; components SNOMED CT `4471000202106`/`4481000202108` + LOINC `8480-6`/`8462-4`<br>**Regel:** positive, sikkert parsbare components publiseres som standard FHIR R4 Observation uten draft canonical |
| protein in urine | DIRECT | NLK `NPU04206` med kodeverk 8340 `T008`/`T052`/`T048`/`T049`/`T050`<br>**Regel:** DHG enum `Neg`, `Spor`, `1+`, `2+`, `3+` oversettes eksplisitt; ukjente values utelates |
| edema | PARTIAL | text-only `Observation.code`, `valueInteger`<br>**Regel:** raw DHG-grad `0..3` beholdes encounter-scoped; betydningen av hvert scale-trinn utledes ikke |
| `fetusesVitalSigns[].fosterId` | DIRECT | minimal pregnancy-scoped `Patient.id`; referert fra `Observation.focus`<br>**Regel:** bare positivt, eksplisitt `fosterId`; ID-en avledes deterministisk fra maternal logical ID og `fosterId`, inneholder ikke NIN, og brukes ikke som clinical identifier |
| `fetusesVitalSigns[].fetalHeartRate` | DIRECT | SNOMED CT `364075005` + LOINC `55283-6`, UCUM `{beats}/min`, `valueQuantity`<br>**Regel:** bare positiv source value fra datert appointment; mor er `subject`, foster-Patient er `focus` |
| `fetusesVitalSigns[].fetalPresentationLie` | DIRECT | text-only `Observation.code`; Volven 8534 i `valueCodeableConcept`<br>**Regel:** source code/system/display beholdes bare når code system er dokumentert Volven 8534; method utledes ikke |
| `fetusesVitalSigns[].motherFeelsBabyMovements` | DIRECT | LOINC `57088-7`, `valueBoolean`<br>**Regel:** eksplisitt maternal report; `false` beholdes og `null` utelates. SNOMED CT `268470003` brukes ikke som tilleggskode fordi den betyr en positiv finding og derfor ikke er state-neutral ved `false` |
| `fetusesVitalSigns[].note` | DIRECT | text-only `Observation.code`, `valueString`<br>**Regel:** trimmet source text beholdes ordrett og tolkes ikke til diagnosis eller finding |
| fetus entry uten positivt `fosterId`, eller uten datert appointment | UNSUPPORTED | —<br>**Regel:** det opprettes verken fetus Patient eller fetus Observation uten entydig pregnancy-scoped identity og temporal context |
| `pointsOfContact.midwife.name` | DIRECT | contained `Practitioner.name.text`, referenced fra contained `PractitionerRole.practitioner`<br>**Regel:** source string trimmes; det gjøres ingen directory lookup |
| `pointsOfContact.midwife.hprNr` | DIRECT | contained `Practitioner.identifier`<br>**Regel:** source string trimmes og publiseres med HPR-system `urn:oid:2.16.578.1.12.4.1.4.4`; identifier konstrueres ikke ved fravær |
| `pointsOfContact.midwife.organizationName` | DIRECT | contained `Organization.name`, referenced fra contained `PractitionerRole.organization`<br>**Regel:** source string trimmes; DHG-kontrakten leverer ikke jordmorens organisasjonsnummer |
| `pointsOfContact.midwife` relationship | DIRECT | contained `PractitionerRole.code.text=Jordmor` i `CareTeam.participant.member`<br>**Regel:** eksplisitt DHG relationship; period, specialty og services utledes ikke |
| `pointsOfContact.generalPractitioner.name` | DIRECT | contained `Practitioner.name.text`, referenced fra contained `PractitionerRole.practitioner`<br>**Regel:** source string trimmes; det gjøres ingen ekstern GP lookup |
| `pointsOfContact.generalPractitioner.hprNr` | DIRECT | contained `Practitioner.identifier`<br>**Regel:** source string trimmes og publiseres med HPR-system `urn:oid:2.16.578.1.12.4.1.4.4`; identifier konstrueres ikke ved fravær |
| `pointsOfContact.generalPractitioner.organizationName` | DIRECT | contained `Organization.name`, referenced fra contained `PractitionerRole.organization`<br>**Regel:** source string trimmes; det gjøres ingen directory lookup |
| `pointsOfContact.generalPractitioner.organizationId` | DIRECT | contained `Organization.identifier`<br>**Regel:** source organization number publiseres med ENH-system `urn:oid:2.16.578.1.12.4.1.4.101`; identifier konstrueres ikke ved fravær |
| `pointsOfContact.generalPractitioner` relationship | DIRECT | contained `PractitionerRole.code.text=Fastlege` i `CareTeam.participant.member`<br>**Regel:** eksplisitt DHG relationship; period, specialty og services utledes ikke |
| `pointsOfContact.maternityHealthcareCentre` | DIRECT | contained `Organization.name` og `Organization.type.text=Helsestasjon` i direkte `CareTeam.participant.member`<br>**Regel:** free-text navn; det konstrueres ingen organization identifier eller managing responsibility |
| `pointsOfContact.birthInstitute`, `birthStatus`, `lastUpdatedBy` | UNSUPPORTED | —<br>**Regel:** feltene er utenfor gjeldende population/FHIR scope; ingen ekstern GP- eller directory lookup |

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
