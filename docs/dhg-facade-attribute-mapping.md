# DHG API to facade attribute mapping

Status: implementation-aligned as of 2026-09-01.

> Language: This low-level attribute catalog is maintained in English to match DHG JSON names and FHIR element names. The shorter operational and mapping documentation is maintained in Norwegian.

This document is the exhaustive attribute catalog for the read-only facade. It traces the
two DHG responses used at runtime through the facade's normalized population model and into
FHIR R4. The shorter [clinical mapping matrix](mapping.md) remains the terminology-oriented
view, while [DHG to FHIR resource mapping](dhg-fhir-resource-mapping.md) describes the
resource shapes and query surface.

The source contract was checked against the official NHN documentation for
[Status](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/statusmd),
[Resources](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/resourcesmd), and
[Metadata](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/metadatamd).
The implemented contract is defined by `DhgModels.cs`, the DHG-to-population transformation
by `DhgPopulationSnapshotFactory.cs`, and the population-to-FHIR transformation by
`FhirPopulationMapper.cs` and `PopulationCodes.cs`.

## Scope and interpretation

- DHG is the only runtime clinical data source.
- The FHIR layer receives normalized population objects and does not know DHG JSON paths.
- `DIRECT` means the explicit DHG fact is represented without changing its meaning.
- `PARTIAL` means only a semantically safe part is represented or the value is conditional.
- `CONTROL` means the attribute controls source selection, authorization, filtering, or
  consistency and is not itself returned as a FHIR data element.
- `UNSUPPORTED` means the DTO accepts the documented attribute for contract tolerance but
  the current facade does not expose it.
- `CONTAINER` means the attribute has no independent FHIR value; its child attributes are
  mapped separately.
- `null` remains unknown/not registered and normally produces no FHIR element or resource.
  A nullable clinical boolean is never collapsed to `false`.
- Text that is mapped is trimmed but not parsed. No diagnosis, medication, relationship,
  procedure, or other clinical fact is inferred from free text.
- A mapped resource with `metadata.enteredInError=true` is omitted in full.

## End-to-end flow and source gating

| DHG operation / JSON path | Facade handoff | FHIR result | Status and exact rule |
|---|---|---|---|
| `GET /status` | `DhgStatusResponse` | None directly | `CONTROL`: always called before the active record. |
| `status.hasGivenConsent` | Consent gate | `OperationOutcome` on failure | `CONTROL`: must be exactly `true`; `false` or `null` stops processing with HTTP 403. |
| `status.deceased` | Availability gate | `OperationOutcome` on failure | `CONTROL`: exactly `true` stops processing with HTTP 403; `false` and `null` do not themselves stop it. |
| `status.hasActiveMaternityRecord` | Active-record gate and `PopulationSnapshot.HasActiveMaternityRecord` | None directly | `CONTROL`: must be exactly `true`; otherwise processing stops with HTTP 404. |
| `status.latestRecordId` | Record selector | DHG record URL only | `CONTROL`: must be a nonblank UUID; selects `GET /record/{latestRecordId}` and must match `record.metadata.recordId`. It is never published as a patient identifier. |
| `status.lastChangedDateTime` | `PopulationSnapshot.SourceLastChanged` | None in the current FHIR surface | `CONTROL`: retained internally; falls back to `record.metadata.recordLastUpdated` when absent. |
| `GET /record/{latestRecordId}` | `DhgMaternityRecord` | `Patient`, `Observation`, `Encounter`, `CareTeam`, and search `Bundle` resources | `CONTROL`: the selected current record is the sole clinical source. |
| `record.metadata.recordId` | Consistency check and pregnancy context for `FetalPatientId` | Indirect input to derived fetus `Patient.id` | `CONTROL/PARTIAL`: must match `status.latestRecordId` and parse as UUID. It is combined with maternal logical ID and positive `fosterId`, SHA-256 hashed, and never exposed raw. |
| `record.metadata.recordStatus.status` | Active-record gate | `OperationOutcome` on failure | `CONTROL`: must equal `ACTIVE` case-insensitively; otherwise processing stops with HTTP 404. |

## Common structures

### Resource metadata

`<resource>` below means a mapped instance of `mother`, `currentPregnancy`,
`previousPregnancies`, `geneticDisorders`, `medicalConditions`, `medication`,
`lifestyleFactors`, `clinicalTests`, `rhesusDNegative`,
`vitalMeasurementsBeforePregnancy`, `symphysisFundalHeights[]`,
`antenatalAppointments[]`, or `pointsOfContact`. The ignored `birthStatus` resource is
listed separately later.

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `<resource>.metadata` | `DhgResourceMetadata` | None independently | `CONTAINER`: child attributes below apply. |
| `<resource>.metadata.id` | Input to normalized resource ID | `Resource.id` for generated Observations, Encounters, and CareTeams | `PARTIAL`: combined with a stable field suffix, invalid FHIR ID characters become `-`, and values longer than 64 characters become a lowercase SHA-256 hex digest. Missing IDs use the internal `dhg-<suffix>` fallback. |
| `<resource>.metadata.version` | DTO only | None | `UNSUPPORTED`: the facade is read-only and does not expose DHG optimistic-concurrency versions. |
| `<resource>.metadata.lastUpdated` | `Population*.LastUpdated` | `Resource.meta.lastUpdated` | `DIRECT`: copied to every FHIR resource derived from that DHG resource. For a fetus observed more than once, the newest appointment timestamp wins. |
| `<resource>.metadata.enteredInError` | Active-resource filter | Entire derived FHIR resource set is absent | `CONTROL`: only explicit `true` filters the DHG resource; `false` and `null` do not. |
| `<resource>.metadata.lastUpdatedBy` | DTO only | None | `UNSUPPORTED`: provenance identity is not published by the current facade. |
| `<resource>.metadata.lastUpdatedBy.userType` | DTO only | None | `UNSUPPORTED`. |
| `<resource>.metadata.lastUpdatedBy.orgNr` | DTO only | None | `UNSUPPORTED`; it is not substituted for a point-of-contact organization identifier. |
| `<resource>.metadata.lastUpdatedBy.orgName` | DTO only | None | `UNSUPPORTED`. |
| `<resource>.metadata.lastUpdatedBy.treatmentFacilityName` | DTO only | None | `UNSUPPORTED`. |
| `<resource>.metadata.lastUpdatedBy.hprNr` | DTO only | None | `UNSUPPORTED`; it is not substituted for a point-of-contact HPR number. |
| `<resource>.metadata.lastUpdatedBy.hprRole` | DTO only | None | `UNSUPPORTED`. |
| `<resource>.metadata.lastUpdatedBy.name` | DTO only | None | `UNSUPPORTED`. |

### Record metadata

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `record.metadata.version` | DTO only | None | `UNSUPPORTED`: no DHG write/version surface is exposed. |
| `record.metadata.recordLastUpdated` | Patient/source timestamp fallback | Maternal `Patient.meta.lastUpdated` when `mother.metadata.lastUpdated` is absent | `PARTIAL`: also becomes `PopulationSnapshot.SourceLastChanged` only when `status.lastChangedDateTime` is absent. |
| `record.metadata.lastUpdated` | DTO only | None | `UNSUPPORTED`: the facade deliberately uses `recordLastUpdated` for record-wide fallback semantics. |
| `record.metadata.lastUpdatedBy` and all child fields | DTO only | None | `UNSUPPORTED`: record updater identity is not exposed. |
| `record.metadata.recordStatus.deliveryDate` | DTO only | None | `UNSUPPORTED`: the facade population is the current active pregnancy only. |
| `record.metadata.recordStatus.liveBirth` | DTO only | None | `UNSUPPORTED`. |
| `record.metadata.recordStatus.terminationDate` | DTO only | None | `UNSUPPORTED`. |

### `CodeAndSystem`

These child rules apply only where a resource-specific row below accepts the expected code
system. A structurally valid code from the wrong system is not mapped.

| DHG JSON attribute | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `code` | `CodedValue.Code` | `Coding.code` | `DIRECT`: required by the mapper; a missing code drops the coded value. |
| `display` | `CodedValue.Display` | `Coding.display` and/or `CodeableConcept.text` | `DIRECT`: optional source display is retained; no display is invented for source-defined lifestyle/language values. |
| `codeSystem` | Normalized `CodedValue.System` | `Coding.system` | `PARTIAL`: known `VOLVEN_*` names used by the facade become their OID URNs; absolute URIs and numeric OIDs can be normalized, but each mapped field still enforces its expected system. Unknown strings are dropped. |
| Any unmapped JSON member captured as `AdditionalProperties` | DTO extension data | None | `UNSUPPORTED`: forward-compatible deserialization does not imply clinical exposure. |

## Root resource coverage

| DHG record attribute | Facade result | Status |
|---|---|---|
| `mother` | Maternal `PopulationPatient` plus social-history Observations | Mapped per table below. |
| `currentPregnancy` | Observations | Mapped per table below. |
| `previousPregnancies` | Observations | Mapped per table below. |
| `geneticDisorders` | Observations | Mapped per table below. |
| `medicalConditions` | Observations | Mapped per table below. |
| `medication` | Observations | Mapped per table below. |
| `lifestyleFactors` | Observations | Mapped per table below. |
| `clinicalTests` | Observations | Mapped per table below. |
| `rhesusDNegative` | Observations | Mapped per table below. |
| `vitalMeasurementsBeforePregnancy` | Observations | Mapped per table below. |
| `symphysisFundalHeights[]` | Observations | Mapped per table below. |
| `antenatalAppointments[]` | Encounters, Observations, and optional minimal fetus Patients | Mapped per table below. |
| `pointsOfContact` | CareTeam with contained resources | Mapped per table below. |
| `birthStatus` | None | `UNSUPPORTED`: current-pregnancy facade scope does not expose delivery/birth outcomes. |

## Mother

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `mother.name` | DTO only | None | `UNSUPPORTED`: maternal identity comes from protected patient context; DHG name is not exposed. |
| `mother.address` | DTO only | None | `UNSUPPORTED`. |
| `mother.postNumber` | DTO only | None | `UNSUPPORTED`. |
| `mother.postName` | DTO only | None | `UNSUPPORTED`. |
| `mother.employedLast6Months` | DTO only | None | `UNSUPPORTED`: employment is outside the facade surface. |
| `mother.employmentPercentage` | DTO only | None | `UNSUPPORTED`. |
| `mother.occupationAndIndustry` | DTO only | None | `UNSUPPORTED`; free text is not parsed. |
| `mother.language.{code,display,codeSystem}` | `PopulationPatient.PreferredLanguage` | `Patient.communication.language`; `communication.preferred=true` | `DIRECT`: emitted only for Volven 3303. |
| `mother.countryOfBirth.{code,display,codeSystem}` | DTO only | None | `UNSUPPORTED`: country/demography is outside the minimal Patient surface. |
| `mother.needsLanguageInterpreter` | `PopulationPatient.NeedsInterpreter` | HL7 `patient-interpreterRequired` extension with `valueBoolean` | `DIRECT`: explicit `false` is retained; `null` omits the extension. |
| `mother.cohabitingCoparent` | `PopulationObservation(BooleanValue)` | Social-history `Observation.valueBoolean`; text-only code `Bor sammen med medforelder` | `DIRECT`: no relationship, parental responsibility, or household membership is inferred. |
| `mother.cohabitingCoparentNote` | `PopulationObservation(TextValue)` | Social-history `Observation.valueString`; text-only code | `PARTIAL`: trimmed source text is retained without semantic parsing. |

The maternal `Patient.id` is not a DHG attribute. In protected GET flows it comes from the
short-lived patient context; in authenticated POST search it is a stable HMAC pseudonym.
Neither variant exposes the national identity number.

## Current pregnancy

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `currentPregnancy.dateLastPeriod` | `PopulationObservation(DateValue)` | `Observation.valueDateTime` (day precision), LOINC `8665-2` | `DIRECT`. |
| `currentPregnancy.dueDate` | `PopulationObservation(DateValue)` | `Observation.valueDateTime`, SNOMED CT `289206005` plus LOINC `11778-8` | `DIRECT`: explicitly the estimate based on last period. |
| `currentPregnancy.dueDateBasedOnUltrasound` | `PopulationObservation(DateValue)` | `Observation.valueDateTime`, SNOMED CT `738070007` plus LOINC `11778-8` | `DIRECT`. |
| `currentPregnancy.dueDateCorrectedDate` | `PopulationObservation(DateValue)` | `Observation.valueDateTime`; text-only code `Korrigert termindato` | `PARTIAL`: retained as a separate source fact; no clinical precedence or correction reason is inferred. |
| `currentPregnancy.hasPrenatalDiagnosticsTests` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`; text-only code `Gitt informasjon om fosterdiagnostikk` | `DIRECT`: represents whether information was provided, not whether a test occurred or its result. |
| `currentPregnancy.numberOfFetuses` | `PopulationObservation(IntegerValue)` | `Observation.valueInteger`, SNOMED CT `246435002` | `PARTIAL`: only positive values are emitted. |
| `currentPregnancy.assistedConception` | `DhgAssistedConception` | None independently | `CONTAINER`. |
| `currentPregnancy.assistedConception.hadAssistedConception` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `813541000000100` | `DIRECT`: explicit `false` is retained. |
| `currentPregnancy.assistedConception.dateAssistedConception` | `PopulationObservation.EffectiveDate` | Assisted-conception `Observation.effectiveDateTime` with day precision | `PARTIAL`: used only when `hadAssistedConception=true`; it never creates an Observation or status by itself. |
| `currentPregnancy.birthPreparationTalk` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `702396006` | `DIRECT`. |
| `currentPregnancy.breastfeedingGuidance` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `243094003` | `DIRECT`. |

## Previous pregnancies

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `previousPregnancies.numberOfPreviousPregnancies` | `PopulationObservation(IntegerValue)` | `Observation.valueInteger`, SNOMED CT `246211005` | `DIRECT`: non-null source count; the facade does not calculate it from other outcomes. |
| `previousPregnancies.numberOfPreviousLiveBirths` | `PopulationObservation(IntegerValue)` | `Observation.valueInteger`, LOINC `11636-8` | `DIRECT`. |
| `previousPregnancies.spontaneousMiscarriages` | `PopulationObservation(IntegerValue)` | `Observation.valueInteger`, SNOMED CT `248989003` | `DIRECT`. |
| `previousPregnancies.stillBirths22weeks` | `PopulationObservation(IntegerValue)` | `Observation.valueInteger`, SNOMED CT `252112002` | `PARTIAL`: the DHG 22-week/500-g threshold remains a source-contract limitation. |
| `previousPregnancies.numberOfEctopicPregnancies` | `PopulationObservation(IntegerValue)` | `Observation.valueInteger`, SNOMED CT `440537001` | `DIRECT`. |
| `previousPregnancies.note` | `PopulationObservation(TextValue)` | `Observation.valueString`; text-only code | `PARTIAL`: no pregnancy outcome, diagnosis, or procedure is extracted. |

There is no explicit induced-abortion attribute. The facade never derives one as a residual
from the counters above.

## Genetic disorders

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `geneticDisorders.noneKnown` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`; text-only code `Ingen kjente arvelige sykdommer` | `DIRECT`: `false` does not establish a disorder. |
| `geneticDisorders.parentsAreRelatives` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `842009` | `DIRECT`. |
| `geneticDisorders.hipDysplasia` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`; text-only family-history code | `PARTIAL`: affected relative and clinical diagnosis are unknown. |
| `geneticDisorders.other` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`; text-only code `Annen arvelig sykdom` | `PARTIAL`: no disorder type is inferred. |
| `geneticDisorders.note` | `PopulationObservation(TextValue)` | `Observation.valueString`; text-only code | `PARTIAL`: no disorder, person, or relationship is extracted. |

## Medical conditions

All boolean rows retain explicit `false` and omit `null`.

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `medicalConditions.nothingParticular` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`; text-only code `Ingenting spesielt` | `DIRECT`: `false` does not identify a disease. |
| `medicalConditions.heartDisease` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `56265001` | `DIRECT`: no subtype is inferred. |
| `medicalConditions.highBloodPressure` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `38341003` | `DIRECT`: no subtype is inferred. |
| `medicalConditions.kidneyUrinaryTractDiseases` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`; exact text-only composite code | `PARTIAL`: not split into kidney and urinary-tract conditions. |
| `medicalConditions.diabetes` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `73211009` | `PARTIAL`: DHG does not distinguish pre-existing from gestational diabetes. |
| `medicalConditions.allergiesAsthma` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`; exact text-only composite code | `PARTIAL`: not split into allergy and asthma. |
| `medicalConditions.epilepsy` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `84757009` | `DIRECT`. |
| `medicalConditions.thrombosis` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `439127006` | `PARTIAL`: DHG combines thrombosis and/or treatment; treatment is not inferred. |
| `medicalConditions.autoimmuneDisease` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `85828009` | `DIRECT`: no subtype is inferred. |
| `medicalConditions.gynecologicalConditions` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`; exact text-only composite code | `PARTIAL`: disease, intervention, and surgery are not split. |
| `medicalConditions.mentalHealth` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `74732009` | `PARTIAL`: no specific diagnosis is inferred. |
| `medicalConditions.other` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`; text-only code | `PARTIAL`: no condition is inferred. |
| `medicalConditions.note` | `PopulationObservation(TextValue)` | `Observation.valueString`; text-only code | `PARTIAL`: no diagnosis, medication, procedure, or affected person is extracted. |

## Medication

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `medication.medicationFrequency` | `PopulationObservation(TextValue)` | `Observation.valueString`; text-only code `Hyppighet av legemiddelbruk` | `PARTIAL`: raw enum/string is retained without normalizing frequency or inferring a medication. |
| `medication.drugAllergy` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `416098002` | `DIRECT`. |
| `medication.folate` | `DhgFolate` | None independently | `CONTAINER`. |
| `medication.folate.takenBefore` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `792807003`; note `Før svangerskapet` | `PARTIAL`: time context is an annotation; it is not inferred from `takenDuring`. |
| `medication.folate.takenDuring` | `PopulationObservation(BooleanValue)` | `Observation.valueBoolean`, SNOMED CT `792807003`; note `Under svangerskapet` | `PARTIAL`: time context is an annotation; it is not inferred from `takenBefore`. |
| `medication.note` | `PopulationObservation(TextValue)` | `Observation.valueString`; text-only code | `PARTIAL`: no medication, dose, indication, or instruction is extracted. |

## Lifestyle factors

One valid frequency creates one social-history Observation. The first-consultation and
week-36 objects can therefore create two Observations for one stimulus.

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `lifestyleFactors.stimuli[]` | Iterated `DhgStimulus` | Zero to two Observations per entry | `CONTAINER`: invalid/missing stimulus coding drops the entry. |
| `lifestyleFactors.stimuli[].stimuliType.{code,display,codeSystem}` | Dynamic `PopulationCode` | `Observation.code` | `DIRECT`: only Volven 8536 is accepted. |
| `lifestyleFactors.stimuli[].stimuliFrequencyFirstConsultation` | `DhgStimuliFrequency` | One Observation when its frequency is valid | `CONTAINER`: annotation identifies `Ved første konsultasjon`. |
| `...stimuliFrequencyFirstConsultation.stimuliFrequency.{code,display,codeSystem}` | `CodedValue` | `Observation.valueCodeableConcept` | `DIRECT`: only Volven 8537 is accepted. |
| `...stimuliFrequencyFirstConsultation.dailyCount` | `PopulationComponent(IntegerValue)` | `Observation.component.valueInteger`; text-only component code `Daglig antall` | `PARTIAL`: only non-negative values are retained; no unit or clinical interpretation is invented. |
| `lifestyleFactors.stimuli[].stimuliFrequencyAtWeek36` | `DhgStimuliFrequency` | One Observation when its frequency is valid | `CONTAINER`: annotation identifies `Ved uke 36`. |
| `...stimuliFrequencyAtWeek36.stimuliFrequency.{code,display,codeSystem}` | `CodedValue` | `Observation.valueCodeableConcept` | `DIRECT`: only Volven 8537 is accepted. |
| `...stimuliFrequencyAtWeek36.dailyCount` | `PopulationComponent(IntegerValue)` | `Observation.component.valueInteger` | `PARTIAL`: only non-negative values are retained; no unit is invented. |
| `lifestyleFactors.note` | `PopulationObservation.Note` | `Observation.note` on each emitted lifestyle Observation | `PARTIAL`: appended to the consultation/week context; no standalone Observation is created and the text is not parsed. |

## Clinical tests

For every laboratory boolean below, `true` becomes Volven 8340 `T002 |Positiv|`, `false`
becomes `T008 |Negativ|`, and `null` creates no Observation. Numeric quantities are emitted
only when positive.

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `clinicalTests.hemoglobin` | `PopulationObservation(QuantityValue)` | NLK `NOR05172`, `valueQuantity` UCUM `g/dL` | `DIRECT`: first-trimester source fact. |
| `clinicalTests.hemoglobinAt3rdTrimester` | `PopulationObservation(QuantityValue)` | NLK `NOR05172`, `valueQuantity` UCUM `g/dL`; third-trimester note | `DIRECT`. |
| `clinicalTests.ferritin` | `PopulationObservation(QuantityValue)` | NLK `NPU19763`, `valueQuantity` UCUM `ug/L` | `DIRECT`. |
| `clinicalTests.hbv` | `PopulationObservation(CodedValue)` | SNOMED CT `165806002`, coded positive/negative result | `DIRECT`: source explicitly identifies HBV surface antigen. |
| `clinicalTests.hbvCore` | `PopulationObservation(CodedValue)` | Text-only analyte code, coded positive/negative result | `PARTIAL`: no unverified analyte coding is invented. |
| `clinicalTests.hiv` | `PopulationObservation(CodedValue)` | Text-only analyte code, coded positive/negative result | `PARTIAL`. |
| `clinicalTests.syphilis` | `PopulationObservation(CodedValue)` | Text-only analyte code, coded positive/negative result | `PARTIAL`. |
| `clinicalTests.aboRh` | `DhgAboRh` | None independently | `CONTAINER`. |
| `clinicalTests.aboRh.aboType` | `CodedValue` | NLK `NPU58582` plus LOINC `883-9`; SNOMED CT coded blood group value | `DIRECT`: only `A`, `B`, `AB`, and letter `O` are accepted. |
| `clinicalTests.aboRh.rhesusDType` | `CodedValue` | NLK `NPU21917` plus LOINC `10331-7`; SNOMED CT RhD value | `DIRECT`: `NEGATIVE`, documented `POSTIVE`, and corrected `POSITIVE` are accepted case-insensitively. |
| `clinicalTests.bloodAntibodies` | `PopulationObservation(CodedValue)` | Text-only analyte code, coded positive/negative result | `PARTIAL`: antibody identity is not inferred. |
| `clinicalTests.chlamydia` | `PopulationObservation(CodedValue)` | Text-only analyte code, coded positive/negative result | `PARTIAL`. |
| `clinicalTests.toxoplasmosis` | `PopulationObservation(CodedValue)` | Text-only analyte code, coded positive/negative result | `PARTIAL`: DHG can cover more than one analyte. |
| `clinicalTests.rubellaAntigen` | `PopulationObservation(CodedValue)` | NLK `NPU12412` P-Rubellavirus IgG, coded positive/negative result | `DIRECT`: mapping follows the documented meaning rather than the misleading JSON name. |
| `clinicalTests.hepatitisC` | `PopulationObservation(CodedValue)` | Text-only analyte code, coded positive/negative result | `PARTIAL`. |
| `clinicalTests.mrsaVreEsbl` | `PopulationObservation(CodedValue)` | Text-only composite code, coded positive/negative result | `PARTIAL`: organism/resistance mechanism is not inferred. |
| `clinicalTests.bHbA1c` | `PopulationObservation(QuantityValue)` | NLK `NPU27300`, `valueQuantity` UCUM `mmol/mol` | `DIRECT`. |
| `clinicalTests.glucoseTolerance` | `DhgGlucoseTolerance` | None independently | `CONTAINER`. |
| `clinicalTests.glucoseTolerance.fastingGlucoseLevel` | `PopulationObservation(QuantityValue)` | SNOMED CT `271062006`, `valueQuantity` UCUM `mmol/L` | `DIRECT`: only positive values; test date becomes `effectiveDateTime` when present. |
| `clinicalTests.glucoseTolerance.post2hGlucoseLevel` | `PopulationObservation(QuantityValue)` | SNOMED CT `49167009`, `valueQuantity` UCUM `mmol/L` | `DIRECT`: only positive values; test date becomes `effectiveDateTime` when present. |
| `clinicalTests.glucoseTolerance.testDate` | `PopulationObservation.EffectiveDate` | `Observation.effectiveDateTime` on mapped fasting and two-hour results | `PARTIAL`: does not create a resource without a mapped glucose result. |
| `clinicalTests.gonorrhea` | `PopulationObservation(CodedValue)` | Text-only analyte code, coded positive/negative result | `PARTIAL`. |
| `clinicalTests.cytomegaloVirus` | `PopulationObservation(CodedValue)` | Text-only analyte code, coded positive/negative result | `PARTIAL`. |
| `clinicalTests.asymptomaticBacteriuria` | `PopulationObservation(CodedValue)` | Text-only analyte code, coded positive/negative result | `PARTIAL`. |
| `clinicalTests.groupBStreptococci` | `PopulationObservation(CodedValue)` | Text-only analyte code, coded positive/negative result | `PARTIAL`. |
| `clinicalTests.note` | `PopulationObservation(TextValue)` | `Observation.valueString`; text-only code | `PARTIAL`: no analyte, result, diagnosis, or assessment is extracted. |

## Rhesus D negative

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `rhesusDNegative.consentFetalRhesusTyping` | DTO only | None | `UNSUPPORTED`: consent is not converted into a clinical Observation; a FHIR Consent surface needs an explicit architecture and policy decision. |
| `rhesusDNegative.fetusRhDPositiveAtWeek24` | `PopulationObservation(CodedValue)` | Laboratory Observation with text-only aggregate code and Volven 8340 positive/negative value | `PARTIAL`: `true` means at least one fetus is RhD-positive; `false` means all tested fetuses are RhD-negative. It is not assigned to one fetus. |
| `rhesusDNegative.prophylaxisAtWeek28` | `PopulationObservation(BooleanValue)` | Therapy Observation, SNOMED CT `408783007`, `valueBoolean` | `DIRECT`. |
| `rhesusDNegative.dateForResult` | `PopulationComponent(DateValue)` | Text-only `Observation.component.valueDateTime` with day precision | `PARTIAL`: included only on an emitted aggregate fetus-RhD result; not treated as specimen, effective, or issued time. |
| `rhesusDNegative.note` | `PopulationObservation(TextValue)` | Laboratory `Observation.valueString`; text-only code | `PARTIAL`: no result, diagnosis, treatment, or assessment is extracted. |

## Vital measurements before pregnancy

All three values are emitted only when positive. DHG provides no measurement timestamp, so
the facade does not construct `effective[x]` and does not claim a specialized Vital Signs
profile.

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `vitalMeasurementsBeforePregnancy.height` | `PopulationObservation(QuantityValue)` | Vital-signs Observation; SNOMED CT `50373000` plus LOINC `8302-2`; UCUM `cm` | `PARTIAL`: source context is retained in `Observation.note`. |
| `vitalMeasurementsBeforePregnancy.prePregnancyWeight` | `PopulationObservation(QuantityValue)` | Vital-signs Observation; SNOMED CT `27113001` plus LOINC `29463-7`; UCUM `kg` | `PARTIAL`. |
| `vitalMeasurementsBeforePregnancy.bMI` | `PopulationObservation(QuantityValue)` | Vital-signs Observation; SNOMED CT `60621009` plus LOINC `39156-5`; UCUM `kg/m2` | `PARTIAL`. |

## Symphysis-fundal heights

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `symphysisFundalHeights[].pregnancyWeek` | DTO only | None | `UNSUPPORTED`: currently not represented or used to derive the measurement date. |
| `symphysisFundalHeights[].measurement` | `PopulationObservation(QuantityValue)` | Vital-signs Observation; SNOMED CT `364253002`; UCUM `cm` | `DIRECT`: only positive measurements are emitted. |
| `symphysisFundalHeights[].measurementDate` | `PopulationObservation.EffectiveDate` | `Observation.effectiveDateTime` with day precision | `PARTIAL`: emitted only with a valid positive measurement. |

## Antenatal appointments and fetus findings

Every appointment not marked `enteredInError=true` creates one `PopulationEncounter`, even
if all clinical attributes are absent. Appointments are sorted by `appointmentDate`; missing
dates sort first. Every appointment-derived Observation references that Encounter.

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `antenatalAppointments[].appointmentDate` | `PopulationEncounter.Date` and `EffectiveDate` | `Encounter.period.start/end` set to the same day; appointment Observations use `effectiveDateTime` | `DIRECT`: when absent, Encounter remains with no period and Observations have no `effective[x]`. |
| `antenatalAppointments[].pregnancyWeek` | Input to gestational-age `QuantityValue` | LOINC `18185-9`, UCUM `d` | `PARTIAL`: must be positive and is combined with a valid day offset as `week * 7 + day`. |
| `antenatalAppointments[].daysAfterFullPregnancyWeek` | Input to gestational-age `QuantityValue` | Same Observation as pregnancy week; original `week+day` is retained in `Observation.note` | `PARTIAL`: must be `0..6`; `null` is treated as zero only when pregnancy week is valid. |
| `antenatalAppointments[].motherWeight` | `PopulationObservation(QuantityValue)` | Vital-signs Observation; SNOMED CT `27113001` plus LOINC `29463-7`; UCUM `kg` | `DIRECT`: only positive values. |
| `antenatalAppointments[].bloodPressure` | Parsed into two `PopulationComponent` values | LOINC `85354-9` panel; systolic/diastolic SNOMED CT plus LOINC components; UCUM `mm[Hg]` | `PARTIAL`: only whitespace-tolerant `NN/NN` or `NNN/NNN` with positive components is emitted; no inference from other text. |
| `antenatalAppointments[].proteinInUrineTestResult` | `CodedValue` | NLK `NPU04206`, `Observation.valueCodeableConcept` | `DIRECT`: exact mappings are `Neg->T008`, `Spor->T052`, `1+->T048`, `2+->T049`, `3+->T050` in Volven 8340; other values are omitted. |
| `antenatalAppointments[].edema` | `PopulationObservation(IntegerValue)` | Exam Observation with text-only code and raw `valueInteger` | `PARTIAL`: only `0..3`; scale-step meaning is not inferred. |
| `antenatalAppointments[].fetusesVitalSigns` | Iterated fetus findings | Optional fetus Patients and fetus-focused Observations | `CONTAINER`: child attributes are mapped below. |
| `antenatalAppointments[].medication` | `PopulationObservation(BooleanValue)` | Encounter-scoped `Observation.valueBoolean`; text-only code | `PARTIAL`: no medication, dose, indication, or treatment status is inferred. |
| `antenatalAppointments[].employmentRate` | DTO only | None | `UNSUPPORTED`: employment is outside the current facade surface. |
| `antenatalAppointments[].note` | `PopulationObservation(TextValue)` | Encounter-scoped `Observation.valueString`; text-only code | `PARTIAL`: no diagnosis, medication, procedure, measurement, or assessment is extracted. |
| `...fetusesVitalSigns[].fosterId` | Optional `PopulationFetusPatient.LogicalId` and Observation focus ID | Minimal fetus `Patient.id`; `Observation.focus=Patient/{derived-id}` | `PARTIAL`: only positive IDs. The ID is SHA-256-derived from maternal logical ID, active record UUID, and source fetus ID. Raw `fosterId` is not exposed. Missing/non-positive IDs do not suppress findings but produce no fetus Patient/focus. |
| `...fetusesVitalSigns[].fetalHeartRate` | `PopulationObservation(QuantityValue)` | Vital-signs Observation; SNOMED CT `364075005` plus LOINC `55283-6`; UCUM `{beats}/min` | `DIRECT`: only positive values; maternal Patient remains `subject`, optional fetus Patient is `focus`. |
| `...fetusesVitalSigns[].fetalPresentationLie.{code,display,codeSystem}` | `PopulationObservation(CodedValue)` | Exam Observation with text-only question code and Volven 8534 `valueCodeableConcept` | `DIRECT`: only Volven 8534 is accepted. |
| `...fetusesVitalSigns[].motherFeelsBabyMovements` | `PopulationObservation(BooleanValue)` | Survey Observation, LOINC `57088-7`, `valueBoolean` | `DIRECT`: explicit `false` is retained; maternal Patient remains subject. |
| `...fetusesVitalSigns[].note` | `PopulationObservation(TextValue)` | Exam `Observation.valueString`; text-only code | `PARTIAL`: no diagnosis or finding is extracted. |

Fetus Patients contain only `id` and optional `meta.lastUpdated`. The facade does not invent
NIN, identifier, name, gender, or birth date.

## Points of contact

When at least one supported value is present, the facade creates one active `CareTeam` with
the maternal Patient as `subject`. Practitioner, PractitionerRole, and Organization resources
are contained within that CareTeam. No Grunndata or directory lookup is performed.

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `pointsOfContact.generalPractitioner` | `PopulationCareTeamMember` | Contained Practitioner/Organization/PractitionerRole as applicable | `CONTAINER`. |
| `pointsOfContact.generalPractitioner.name` | `PopulationCareTeamMember.Name` | Contained `Practitioner.name.text` | `DIRECT`: trimmed; no external lookup. |
| `pointsOfContact.generalPractitioner.organizationName` | `PopulationCareTeamMember.OrganizationName` | Contained `Organization.name` | `DIRECT`. |
| `pointsOfContact.generalPractitioner.organizationId` | `PopulationCareTeamMember.OrganizationId` | Contained `Organization.identifier` with ENH OID system | `DIRECT`: source-provided value only. |
| `pointsOfContact.generalPractitioner.hprNr` | `PopulationCareTeamMember.HprNumber` | Contained `Practitioner.identifier` with HPR OID system | `DIRECT`: source-provided value only. |
| `pointsOfContact.midwife` | `PopulationCareTeamMember` | Contained Practitioner/Organization/PractitionerRole as applicable | `CONTAINER`. |
| `pointsOfContact.midwife.name` | `PopulationCareTeamMember.Name` | Contained `Practitioner.name.text` | `DIRECT`. |
| `pointsOfContact.midwife.organizationName` | `PopulationCareTeamMember.OrganizationName` | Contained `Organization.name` | `DIRECT`. |
| `pointsOfContact.midwife.hprNr` | `PopulationCareTeamMember.HprNumber` | Contained `Practitioner.identifier` with HPR OID system | `DIRECT`. |
| `pointsOfContact.midwife.organizationId` | DTO contract-tolerance property only | None | `UNSUPPORTED`: NHN documents the midwife shape without organization ID, and the facade does not expose it if present. |
| `pointsOfContact.birthInstitute` | `PopulationCareTeam.BirthInstitute` | Contained Organization name/type and CareTeam participant role `Fødeinstitusjon` | `DIRECT`: trimmed source name; no identifier is invented. |
| `pointsOfContact.maternityHealthcareCentre` | `PopulationCareTeam.MaternityHealthcareCentre` | Contained Organization name/type `Helsestasjon` and CareTeam participant | `DIRECT`: trimmed source name; no identifier is invented. |

The contained PractitionerRole text is `Fastlege` or `Jordmor`, based only on the explicit
DHG relationship. Period, specialty, services, and managing responsibility are not inferred.

## Birth status

The whole resource is retained in the DTO so the facade can deserialize the complete active
record response, but it is outside the current-pregnancy population surface.

| DHG JSON path | Facade handoff | FHIR output | Status and exact rule |
|---|---|---|---|
| `birthStatus.metadata` and all metadata children | DTO only | None | `UNSUPPORTED`. |
| `birthStatus.birthStatus[]` | DTO only | None | `UNSUPPORTED`. |
| `birthStatus.birthStatus[].fosterId` | DTO only | None | `UNSUPPORTED`: it is not used to create or correlate fetus Patients. |
| `birthStatus.birthStatus[].status.{code,display,codeSystem}` | DTO only | None | `UNSUPPORTED`. |
| `birthStatus.birthStatus[].datetime` | DTO only | None | `UNSUPPORTED`. |

## Normalized facade fields to common FHIR elements

This final projection is independent of DHG JSON structure.

| Normalized facade field | FHIR R4 element |
|---|---|
| `PopulationPatient.LogicalId` | `Patient.id` |
| `PopulationPatient.LastUpdated` | `Patient.meta.lastUpdated` |
| `PopulationFetusPatient.LogicalId` | Fetus `Patient.id` |
| `PopulationFetusPatient.LastUpdated` | Fetus `Patient.meta.lastUpdated` |
| `PopulationObservation.Id` | `Observation.id` |
| `PopulationObservation.LastUpdated` | `Observation.meta.lastUpdated` |
| `PopulationObservation.Code` | `Observation.code`; supplemental safe codings may be added by `PopulationCodes.CodingsFor` |
| `PopulationObservation.Category` | `Observation.category` using the standard observation-category system |
| `PopulationObservation.Value` | Matching `valueBoolean`, `valueInteger`, `valueDecimal`, `valueDateTime`, `valueString`, `valueCodeableConcept`, or `valueQuantity` |
| `PopulationObservation.Effective` | `Observation.effectiveDateTime` |
| `PopulationObservation.Components` | `Observation.component` |
| `PopulationObservation.EncounterId` | `Observation.encounter` |
| `PopulationObservation.FocusPatientId` | `Observation.focus` |
| `PopulationObservation.Note` | `Observation.note` |
| Maternal logical ID for every Observation | `Observation.subject=Patient/{maternal-id}` |
| `PopulationEncounter.Id` | `Encounter.id` |
| `PopulationEncounter.LastUpdated` | `Encounter.meta.lastUpdated` |
| `PopulationEncounter.Date` | Same day in `Encounter.period.start` and `.end` |
| Facade Encounter constants | `Encounter.status=unknown`, `Encounter.class=AMB`, maternal Patient as `subject` |
| `PopulationCareTeam.Id` | `CareTeam.id` |
| `PopulationCareTeam.LastUpdated` | `CareTeam.meta.lastUpdated` |
| Facade CareTeam constants | `CareTeam.status=active`, maternal Patient as `subject` |

Search Bundles add no DHG attributes. They wrap the mapped resources as FHIR `searchset`
entries, calculate `Bundle.total`, stamp the response time, and optionally construct `fullUrl`
from the facade service base.

## Maintenance rule

When the DHG contract or facade mapping changes, update this catalog in the same change as:

1. `DhgModels.cs` for source-contract changes;
2. `DhgPopulationSnapshotFactory.cs` and mapping tests for normalization changes;
3. `PopulationCodes.cs` and terminology evidence for coding changes; and
4. `FhirPopulationMapper.cs`, FHIR examples, and contract tests for output-shape changes.

An attribute appearing in NHN Swagger or `AdditionalProperties` is not automatically safe to
publish. It needs an explicit row, semantic review, implementation, and test before it becomes
part of the facade contract.
