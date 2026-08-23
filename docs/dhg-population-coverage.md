# DHG-kontrakt for population coverage

Fasaden eksponerer bare `Patient`, `Observation` og `Encounter`. Den implementerer ikke `$populate`, Questionnaire processing, demographics lookup, GP lookup, Grunndata eller andre clinical data sources.

## Consumer contract

- `Patient/{id}` er minimal og inneholder ikke NIN, navn, adresse, birth date, GP eller contact information.
- GET Observation search krever `patient={logical-id}` og aksepterer valgfritt `code`, `category` og day-precision `date`. POST `_search` bruker `patient.identifier` i form body, støtter de samme filtrene og krever HelseID utenfor lokal `DevelopmentTestMode`.
- En manglende eller `null` DHG-verdi produserer ingen Observation. Eksplisitt `false` beholdes; DHG laboratory results bruker kodeverk 8340 `T008 |Negativ|`, mens andre booleans bruker `valueBoolean=false`.
- `metadata.enteredInError=true` produserer ingen FHIR resource.
- `meta.lastUpdated` kommer fra DHG source metadata når de er tilgjengelige.
- Gestational age bruker LOINC `18185-9` og ett UCUM-day Quantity per datert appointment. Fasaden oppretter ikke en ekstra facade-specific «latest» Observation; consumer kan velge nyeste `effectiveDateTime`.
- Etter vellykket patient selection returnerer search uten clinical treff en FHIR `searchset` Bundle med `total=0`.

## Publiserte terminology systems

Fasaden publiserer ikke egne clinical codes under `urn:nhn:population-data`. `Observation.code` og coded values bruker bare mappings som er verifisert mot en autoritativ source:

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

`code` search matcher alle publiserte `Observation.code.coding` entries.

## Viktige query concepts

| Fact | `system|code` |
|---|---|
| last menstrual period | `http://loinc.org|8665-2` |
| gestational age | `http://loinc.org|18185-9` |
| body weight | `http://snomed.info/sct|27113001` eller `http://loinc.org|29463-7` |
| blood pressure panel | `http://loinc.org|85354-9` |
| estimated delivery date | `http://loinc.org|11778-8` |

Blood pressure components bruker norske SNOMED CT-koder `4471000202106` og `4481000202108` sammen med LOINC `8480-6` og `8462-4`. Panelkoden forblir LOINC `85354-9` fordi det ikke er verifisert en entydig norsk SNOMED CT panelkode.

## Eksplisitt unsupported eller partial

- Medication name/dose, diagnosis og andre clinical facts trekkes ikke ut fra free text.
- Combined DHG fields som `allergiesAsthma` og `mrsaVreEsbl` splittes ikke og får ingen misvisende standard code.
- Consent og fetal RhD result eksponeres ikke som mother-subject Observation; de krever en egen FHIR resource/subject decision.
- Stimulus `dailyCount` eksponeres ikke før en semantic standard mapping er godkjent.
- Contact/demographic data og birth-status er utenfor gjeldende API surface.
- Ukjente source fields, code systems og enum values tolereres i DTO, men eksponeres ikke automatisk.
- Blood pressure eksponeres bare når dokumentert `systolic/diastolic` format kan parses sikkert.
- Numeric values med DHG positivity constraint utelates når de er `0` eller negative. Dette innfører ingen clinical reference ranges.
- Edema grade og fetus-spesifikke facts eksponeres ikke før henholdsvis scale semantics og en strukturert FHIR `focus`-strategi er godkjent.
- Pre-pregnancy height, weight og BMI holdes tilbake fordi DHG ikke leverer measurement time. Assisted-conception fields holdes tilbake til en norsk SNOMED CT-mapping er verifisert.

Full field classification finnes i [mapping.md](mapping.md). Query-eksempler finnes i [examples/fhir-queries.md](../examples/fhir-queries.md). Terminology og units krever fortsatt godkjenning fra clinical terminology owner før DHG Test/Production.
