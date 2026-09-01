# Verifiserte DHG→FHIR-beslutninger

Kontrolldato: 2026-08-23.

- DHG er eneste kliniske datakilde ved kjøring. Bare den aktive posten fra `/status` og `/record/{latestRecordId}` brukes.
- `null` utelates. Eksplisitt `false` beholdes.
- Hemoglobin bruker NLK `NOR05172` og UCUM `g/dL`. Ferritin bruker `NPU19763`. ABO og RhD bruker `NPU58582` og `NPU21917`.
- Prøveresultater uten verifisert analyttkode bruker den presise DHG-termen i `Observation.code.text`; fasaden konstruerer ikke egne kliniske koder.
- Fritekst tolkes ikke som diagnose, legemiddel, dose, analytt, prosedyre eller berørt person.
- Svangerskapsalder lagres per konsultasjon med LOINC `18185-9` og UCUM `d`. Manglende konsultasjonsdato gir ingen `effective[x]`.
- Positiv `fosterId` gir en minimal fosterressurs. Fosterfunn bruker mor som `subject` og fosteret som `focus`. Uten positiv `fosterId` beholdes funnet uten `focus`.
- `dailyCount` beholdes som en heltallskomponent uten konstruert enhet. Ødemgrad beholdes som heltall fra 0 til 3 uten klinisk fortolkning.
- Høyde, vekt og BMI før svangerskapet eksponeres som FHIR R4-observasjoner uten `effective[x]`, fordi DHG ikke leverer måletidspunkt.
- Fastlege, jordmor, helsestasjon og fødeinstitusjon fra DHG eksponeres i `CareTeam`. Fasaden gjør ingen katalogoppslag og konstruerer ingen manglende identifikatorer.
- Alle observasjoner bruker basisressursen FHIR R4 `Observation`. Fasaden erklærer ingen egne profiler i `meta.profile`.

Detaljene og feltklassifiseringen står i [mappingmatrisen](mapping.md) og [attributtmappingen](dhg-facade-attribute-mapping.md).
