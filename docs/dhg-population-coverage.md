# Dekning av DHG-data

Fasaden eksponerer `Patient`, `Observation`, `Encounter` og `CareTeam`. Den implementerer ikke `$populate`, spørreskjemabehandling, demografioppslag, fastlegeoppslag, Grunndata eller andre kliniske datakilder.

## Implementert dekning

- `Patient/{id}` returnerer mor eller en minimal fosterressurs. Ingen av dem inneholder fødselsnummer.
- `CareTeam` bruker fastlege, jordmor, helsestasjon og fødeinstitusjon fra DHG `pointsOfContact`.
- Observation GET-søk krever `patient` og støtter `code`, `category` og dagspresis `date`.
- Observation POST `_search` bruker `patient.identifier` i forespørselskroppen og støtter de samme filtrene.
- Manglende eller `null` DHG-verdi gir ingen observasjon. Eksplisitt `false` beholdes.
- `metadata.enteredInError=true` utelater hele ressursen.
- `metadata.lastUpdated` mappes til `meta.lastUpdated`.
- Svangerskapsalder mappes med LOINC `18185-9` og UCUM `d` per konsultasjon.
- Høyde, vekt og BMI før svangerskapet eksponeres uten `effective[x]`, fordi DHG ikke leverer måletidspunkt.
- En positiv `fosterId` oppretter en fosterressurs og brukes i `Observation.focus`. Uten positiv `fosterId` beholdes fosterfunnet uten `focus`.
- En konsultasjon uten dato gir `Encounter` uten `period` og observasjoner uten `effective[x]`.
- En ikke-negativ `dailyCount` eksponeres som heltallskomponent uten konstruert enhet.
- Ødemgrad eksponeres som heltall fra 0 til 3 uten klinisk fortolkning.
- Korrigert termindato beholdes som en separat dato og overstyrer ikke andre termindatoer.
- Fritekst beholdes bare i uttrykkelig støttede tekstfelt og tolkes ikke som diagnose, legemiddel, dose, prosedyre eller prøveresultat.
- Et søk uten treff returnerer en `searchset`-`Bundle` med `total=0`.

## Kodeverk

| System | FHIR-URI | Bruk |
|---|---|---|
| LOINC | `http://loinc.org` | Internasjonale observasjonskoder |
| SNOMED CT | `http://snomed.info/sct` | Kliniske funn og begreper med entydig mapping |
| NLK | `urn:oid:2.16.578.1.12.4.1.1.7280` | Norske laboratoriekoder |
| Volven | `urn:oid:2.16.578.1.12.4.1.1.*` | Nasjonale kodeverk |
| UCUM | `http://unitsofmeasure.org` | Måleenheter |

`CareTeam` bruker HPR-systemet `urn:oid:2.16.578.1.12.4.1.4.4` og organisasjonsnummersystemet `urn:oid:2.16.578.1.12.4.1.4.101` når verdiene kommer fra DHG.

## Sentrale søkekoder

| Opplysning | `system|code` |
|---|---|
| Siste menstruasjon | `http://loinc.org|8665-2` |
| Svangerskapsalder | `http://loinc.org|18185-9` |
| Kroppsvekt | `http://snomed.info/sct|27113001` eller `http://loinc.org|29463-7` |
| Kroppshøyde | `http://snomed.info/sct|50373000` eller `http://loinc.org|8302-2` |
| BMI | `http://snomed.info/sct|60621009` eller `http://loinc.org|39156-5` |
| Blodtrykkspanel | `http://loinc.org|85354-9` |
| Termin | `http://loinc.org|11778-8` |
| Fosterets hjertefrekvens | `http://snomed.info/sct|364075005` eller `http://loinc.org|55283-6` |
| Rapporterte fosterbevegelser | `http://loinc.org|57088-7` |

Full feltklassifisering står i [mappingmatrisen](mapping.md).
