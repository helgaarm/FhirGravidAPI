# DHG API to FHIR R4 resource mapping

## Purpose and status

This document describes the FHIR R4 resources the facade currently creates from the active Digitalt helsekort for gravide (DHG) record. It is an implementation contract, not a list of hypothetical resources. A FHIR resource is created only when the stated source data and gating conditions are satisfied.

The detailed field-classification rationale remains in [mapping.md](mapping.md). Consumer-visible coverage and stable query concepts are documented in [dhg-population-coverage.md](dhg-population-coverage.md).

## Source flow and gating

The facade uses two DHG operations for every patient-data request:

| DHG operation/field | Use in the facade | FHIR output |
|---|---|---|
| `GET /status` | Checks consent, deceased status, active-record status and obtains `latestRecordId` | No direct resource |
| `hasGivenConsent` | Must be explicitly `true` before `/record` is called | Otherwise `OperationOutcome` / HTTP 403 |
| `deceased` | Must not be `true` | Otherwise `OperationOutcome` / HTTP 403 |
| `hasActiveMaternityRecord` | Must be explicitly `true` | Otherwise `OperationOutcome` / HTTP 404 |
| `latestRecordId` | Selects the current maternity record | Never exposed as a FHIR identifier or telemetry value |
| `lastChangedDateTime` | Snapshot-level freshness information | Not currently emitted as a separate resource |
| `GET /record/{latestRecordId}` | Supplies the maternity-record content | Mapped as described below |
| `metadata.recordId` | Must equal `latestRecordId` | Mismatch produces `OperationOutcome` / HTTP 503 |
| `metadata.recordStatus.status` | Must equal `ACTIVE` | Otherwise `OperationOutcome` / HTTP 404 |

There is no fallback data source and no persistent clinical cache.

## FHIR resource inventory

| FHIR resource | Cardinality per successful request | Source | Endpoint |
|---|---:|---|---|
| `CapabilityStatement` | 1 | Static facade capability | `GET /fhir/metadata` |
| `Patient` | 1 | Logical patient context plus the safe subset of `mother` | `GET /fhir/Patient/{id}` |
| `Observation` | 0..* | Explicit DHG clinical fields | `GET /fhir/Observation?patient={id}[&code={system}\|{code}]` |
| `Encounter` | 0..* | Dated, non-error antenatal appointments | `GET /fhir/Encounter?patient={id}` |
| `Bundle` | 1 | Search wrapper for Observation or Encounter results | Observation and Encounter search endpoints |
| `OperationOutcome` | 0..1 | Controlled facade, HelseID or DHG error translation | Handled failures on the mapped FHIR endpoints |

Only `Patient`, `Observation`, and `Encounter` are clinical resources in the current capability statement.

## Common mapping rules

- A DHG resource with `metadata.enteredInError=true` creates no FHIR resource.
- A nullable DHG boolean creates no Observation when `null`; explicit `false` becomes `valueBoolean=false`.
- Source `metadata.lastUpdated` becomes `meta.lastUpdated` when present.
- Source measurement dates become `effectiveDate`; date-time values become `effectiveDateTime` when supported.
- Every Observation references `Patient/{logical-id}`.
- Appointment-derived Observations also reference their generated Encounter.
- Observation status is `unknown` because DHG does not provide a FHIR-equivalent result status.
- Encounter status is `unknown` because DHG does not distinguish planned from completed appointments.
- Stable facade-owned concepts use `urn:nhn:population-data`.
- Verified NLK concepts use `urn:oid:2.16.578.1.12.4.1.1.7280`; verified Volven systems use their corresponding `urn:oid:` value.
- Quantities use UCUM `http://unitsofmeasure.org`.
- FHIR IDs normally use a DHG resource metadata ID plus a suffix, sanitized to the FHIR ID character and length rules. Missing metadata IDs fall back to `dhg`; repeated list items also include their source-array position. Those list-item IDs therefore remain stable only while the source order remains stable.

## Patient

| DHG/context source | FHIR element | Rule |
|---|---|---|
| Protected patient context logical ID | `Patient.id` | Never derived from NIN |
| `mother.metadata.lastUpdated` or record update time | `Patient.meta.lastUpdated` | Present only when source time exists |
| `mother.language` | `Patient.communication.language` | Source system/code/display preserved; marked preferred |
| `mother.needsLanguageInterpreter` | Extension `urn:nhn:population-data:StructureDefinition/needs-language-interpreter` | `valueBoolean`; omitted when null |

The Patient does not contain NIN, name, address, birth date, country of birth, employment information, GP or other contact data. `Patient.active` is not asserted because DHG does not provide that fact.

## Encounter

One Encounter is created for each non-error `antenatalAppointments[]` item that has `appointmentDate`.

| DHG field | FHIR element | Mapping |
|---|---|---|
| appointment metadata ID | `Encounter.id` | Stable sanitized ID |
| appointment metadata update time | `Encounter.meta.lastUpdated` | Source timestamp |
| `appointmentDate` | `Encounter.period.start` and `.end` | Same calendar date |
| logical patient ID | `Encounter.subject` | `Patient/{logical-id}` |
| — | `Encounter.class` | `AMB` / ambulatory |
| no equivalent source status | `Encounter.status` | `unknown` |

## Observations by DHG area

Unless another system is shown, the Observation code uses `urn:nhn:population-data`.

### Current pregnancy

| DHG field | Observation code | FHIR value |
|---|---|---|
| `dateLastPeriod` | `date-last-period` | `valueDate` |
| `dueDate` | `due-date-last-period` | `valueDate` |
| `dueDateBasedOnUltrasound` | `due-date-ultrasound` | `valueDate` |
| `numberOfFetuses` | `number-of-fetuses` | `valueInteger` |
| `assistedConception.hadAssistedConception` | `assisted-conception` | `valueBoolean` |
| `assistedConception.dateAssistedConception` | `assisted-conception-date` | `valueDate` |
| `hasPrenatalDiagnosticsTests` | `prenatal-diagnostics-information` | `valueBoolean` |
| `birthPreparationTalk` | `birth-preparation-talk` | `valueBoolean` |
| `breastfeedingGuidance` | `breastfeeding-guidance` | `valueBoolean` |

`dueDateCorrectedDate` is not exposed because the clinical precedence and reason are not sufficiently defined for this facade.

### Previous pregnancies

| DHG field | Observation code | FHIR value |
|---|---|---|
| `numberOfPreviousPregnancies` | `previous-pregnancies` | `valueInteger` |
| `numberOfPreviousLiveBirths` | `previous-live-births` | `valueInteger` |
| `spontaneousMiscarriages` | `spontaneous-miscarriages` | `valueInteger` |
| `stillBirths22weeks` | `stillbirths-22-weeks` | `valueInteger` |
| `numberOfEctopicPregnancies` | `ectopic-pregnancies` | `valueInteger` |
| `note` | `previous-pregnancy-note` | `valueString`, unparsed |

No induced-abortion value is inferred from the counters.

### Genetic disorders and medical conditions

| DHG field group | Observation code | FHIR value |
|---|---|---|
| `geneticDisorders.noneKnown` | `genetic-none-known` | `valueBoolean` |
| `parentsAreRelatives` | `parents-are-relatives` | `valueBoolean` |
| `hipDysplasia` | `hip-dysplasia` | `valueBoolean` |
| `other` | `other-genetic-disorder` | `valueBoolean` |
| genetic `note` | `genetic-note` | `valueString`, unparsed |
| each explicit `medicalConditions` boolean | `medical-condition-{field}` | `valueBoolean` |
| medical `note` | `medical-conditions-note` | `valueString`, unparsed |

Medical-condition suffixes are `nothing-particular`, `heart-disease`, `high-blood-pressure`, `kidney-urinary-tract`, `diabetes`, `allergies-asthma`, `epilepsy`, `thrombosis`, `autoimmune-disease`, `gynecological-conditions`, `mental-health`, and `other`. The combined `allergiesAsthma` fact is never split into separate diagnoses.

### Medication and folate

| DHG field | Observation code | FHIR value/category |
|---|---|---|
| `medicationFrequency` | `medication-frequency` | `valueCodeableConcept`, `therapy` |
| `drugAllergy` | `drug-allergy` | `valueBoolean` |
| `folate.takenBefore` | `folate-before-pregnancy` | `valueBoolean` |
| `folate.takenDuring` | `folate-during-pregnancy` | `valueBoolean` |

The medication note can be retained as an annotation on the frequency Observation, but it is never parsed into a medicine name, dose, `Medication`, or `MedicationStatement`.

### Lifestyle factors

Each `lifestyleFactors.stimuli[]` item with a source code creates one `social-history` Observation:

- `Observation.code` and `valueCodeableConcept` preserve the source stimulus code, normally Volven 8536.
- Components preserve first-consultation and week-36 frequency codes, normally Volven 8537.
- Daily counts become integer components.
- The source note may be retained as an unparsed annotation.

### Clinical tests

| DHG field | Observation code system/code | FHIR value/unit |
|---|---|---|
| `hemoglobin` | facade `hemoglobin-first-trimester` | `valueQuantity`, UCUM `g/dL` |
| `hemoglobinAt3rdTrimester` | facade `hemoglobin-third-trimester` | `valueQuantity`, UCUM `g/dL` |
| `ferritin` | NLK `NPU19763` | `valueQuantity`, UCUM `ug/L` |
| `hbv` | facade `hbv-s-antigen-positive` | `valueBoolean` |
| `hbvCore` | facade `hbv-core-antibody-positive` | `valueBoolean` |
| `hiv` | NLK `NPU19649` | `valueBoolean` |
| `syphilis` | NLK `NPU03611` | `valueBoolean` |
| `aboRh.aboType` | facade `abo-blood-type` | `valueCodeableConcept` |
| `aboRh.rhesusDType` | facade `maternal-rhesus-d` | `valueCodeableConcept` |
| `bloodAntibodies` | facade `blood-antibodies` | `valueBoolean` |
| `chlamydia` | NLK `NPU12331` | `valueBoolean` |
| `toxoplasmosis` | facade `toxoplasmosis-positive` | `valueBoolean` |
| `rubellaAntigen` | NLK `NPU12412` | `valueBoolean` |
| `hepatitisC` | NLK `NPU12033` | `valueBoolean` |
| `mrsaVreEsbl` | facade `mrsa-vre-esbl` | `valueBoolean` |
| `bHbA1c` | NLK `NPU27300` | `valueQuantity`, UCUM `mmol/mol` |
| `glucoseTolerance.fastingGlucoseLevel` | facade `glucose-tolerance-fasting` | `valueQuantity`, UCUM `mmol/L`; test date as `effectiveDate` |
| `glucoseTolerance.post2hGlucoseLevel` | facade `glucose-tolerance-2h` | `valueQuantity`, UCUM `mmol/L`; test date as `effectiveDate` |
| `gonorrhea` | facade `gonorrhea` | `valueBoolean` |
| `cytomegaloVirus` | facade `cytomegalovirus` | `valueBoolean` |
| `asymptomaticBacteriuria` | facade `asymptomatic-bacteriuria` | `valueBoolean` |
| `groupBStreptococci` | NLK `NPU18725` | `valueBoolean` |

The general clinical-tests note is not attached to individual results. Facade codes are used where the DHG boolean does not identify one unambiguous laboratory analysis.

### Rhesus D negative pathway

| DHG field | Observation code | FHIR value |
|---|---|---|
| `consentFetalRhesusTyping` | `rhd-consent-fetal-typing` | `valueBoolean` |
| `fetusRhDPositiveAtWeek24` | `fetus-rhd-week-24` | `valueBoolean`; `dateForResult` as `effectiveDate` |
| `dateForResult` | `fetus-rhd-result-date` | `valueDate` |
| `prophylaxisAtWeek28` | `rhd-prophylaxis-week-28` | `valueBoolean` |

### Measurements before pregnancy and symphysis-fundal height

| DHG field | Observation code | FHIR value/category |
|---|---|---|
| `height` | `pre-pregnancy-height` | `valueQuantity` UCUM `cm`, `vital-signs` |
| `prePregnancyWeight` | `pre-pregnancy-weight` | `valueQuantity` UCUM `kg`, `vital-signs` |
| `bMI` | `pre-pregnancy-bmi` | `valueDecimal`, `vital-signs` |
| `symphysisFundalHeights[].measurement` | `symphysis-fundal-height` | `valueQuantity` UCUM `cm`, `vital-signs` |
| SFH `measurementDate` | — | Observation `effectiveDate` |
| SFH `pregnancyWeek` | component `gestational-weeks` | component `valueInteger` |

### Antenatal appointment observations

Every item below is dated with `appointmentDate` and references the corresponding Encounter.

| DHG field | Observation code | FHIR value/category |
|---|---|---|
| `pregnancyWeek` + `daysAfterFullPregnancyWeek` | `gestational-age-at-appointment` | `valueString` `week+day` plus integer components, `survey` |
| latest dated appointment with gestational age | `recorded-gestational-age` | Same representation; maximum one per snapshot |
| `motherWeight` | `mother-weight` | `valueQuantity` UCUM `kg`, `vital-signs` |
| parseable `bloodPressure` | `blood-pressure` | Original `valueString` plus systolic/diastolic Quantity components in `mm[Hg]`, `vital-signs` |
| `proteinInUrineTestResult` | NLK `NPU04206` | `valueCodeableConcept` from Volven 8340, `laboratory` |
| `edema` | `edema` | `valueInteger`, `exam` |
| each fetus `fetalHeartRate` | `fetal-heart-rate` | `valueQuantity` UCUM `/min`, `vital-signs` |
| each fetus `fetalPresentationLie` | `fetal-presentation-lie` | `valueCodeableConcept` preserving source code, `exam` |
| each fetus `motherFeelsBabyMovements` | `mother-feels-baby-movements` | `valueBoolean`, `exam` |

Appointment medication flag, employment rate, appointment note and fetus note are not currently exposed because their safe consumer semantics are not defined.

## Search Bundles

Observation and Encounter searches always return a FHIR `Bundle` with:

- `type=searchset`;
- `total` equal to the number of matching resources;
- one `entry` per resource with `search.mode=match`;
- an absolute `fullUrl` derived from the request-observed scheme, host and path base;
- `timestamp` set to the facade response time, not the DHG source freshness time;
- `total=0` and no entries when a supported query has no registered value.

The optional Observation `code` filter uses exact `system|code` matching. Absence of an Observation is not equivalent to `false`.

## Resources deliberately not created

| Potential FHIR resource | Current decision |
|---|---|
| `Questionnaire`, `QuestionnaireResponse` | Not part of the generic facade; no questionnaire/linkId coupling |
| `$populate` output | Not implemented; an external SDC engine queries the facade |
| `Medication`, `MedicationStatement` | Medication note is not parsed into clinical medication facts |
| `Condition` | DHG booleans/notes are not promoted to diagnoses |
| `Practitioner`, `PractitionerRole`, `Organization` | `pointsOfContact` is outside the population-data surface |
| Demographic extensions/resources | No demographics source is allowed and sensitive mother fields are not exposed |
| Birth/postpartum resources | `birthStatus` is outside the active-pregnancy first release |
| `Provenance` | `lastUpdatedBy` identity/organization details are not exposed |

Adding any new resource type requires an explicit approved mapping, capability-statement update, privacy/clinical review, documentation update and semantic tests.
