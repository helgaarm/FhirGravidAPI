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
- `birthStatus` eksponerer Volven 8522-status og leveringstid. Positiv `fosterId` korreleres med samme fosterressurs som konsultasjonsfunn.
- En konsultasjon uten dato gir `Encounter` uten `period` og observasjoner uten `effective[x]`.
- En ikke-negativ `dailyCount` eksponeres som heltallskomponent uten konstruert enhet.
- Ødemgrad eksponeres som heltall fra 0 til 3 uten klinisk fortolkning.
- Korrigert termindato beholdes som en separat dato og overstyrer ikke andre termindatoer.
- Fritekst beholdes bare i uttrykkelig støttede tekstfelt og tolkes ikke som diagnose, legemiddel, dose, prosedyre eller prøveresultat.
- Et søk uten treff returnerer en `searchset`-`Bundle` med `total=0`.

## Kodeverk

<table>
  <thead>
    <tr><th width="33.33%" scope="col">System</th><th width="33.33%" scope="col">FHIR-URI</th><th width="33.33%" scope="col">Bruk</th></tr>
  </thead>
  <tbody>
    <tr><td>LOINC</td><td><code>http:/<wbr>/<wbr>loinc.<wbr>org</code></td><td>Internasjonale observasjonskoder</td></tr>
    <tr><td>SNOMED CT</td><td><code>http:/<wbr>/<wbr>snomed.<wbr>info/<wbr>sct</code></td><td>Kliniske funn og begreper med entydig mapping</td></tr>
    <tr><td>NLK</td><td><code>urn:oid:2.<wbr>16.<wbr>578.<wbr>1.<wbr>12.<wbr>4.<wbr>1.<wbr>1.<wbr>7280</code></td><td>Norske laboratoriekoder</td></tr>
    <tr><td>Volven</td><td><code>urn:oid:2.<wbr>16.<wbr>578.<wbr>1.<wbr>12.<wbr>4.<wbr>1.<wbr>1.<wbr>*</code></td><td>Nasjonale kodeverk</td></tr>
    <tr><td>UCUM</td><td><code>http:/<wbr>/<wbr>unitsofmeasure.<wbr>org</code></td><td>Måleenheter</td></tr>
  </tbody>
</table>

`CareTeam` bruker HPR-systemet `urn:oid:2.16.578.1.12.4.1.4.4` og organisasjonsnummersystemet `urn:oid:2.16.578.1.12.4.1.4.101` når verdiene kommer fra DHG.

## Sentrale søkekoder

<table>
  <thead>
    <tr><th width="50%" scope="col">Opplysning</th><th width="50%" scope="col"><code>system|code</code></th></tr>
  </thead>
  <tbody>
    <tr><td>Siste menstruasjon</td><td><code>http:/<wbr>/<wbr>loinc.<wbr>org|8665-2</code></td></tr>
    <tr><td>Svangerskapsalder</td><td><code>http:/<wbr>/<wbr>loinc.<wbr>org|18185-9</code></td></tr>
    <tr><td>Kroppsvekt</td><td><code>http:/<wbr>/<wbr>snomed.<wbr>info/<wbr>sct|27113001</code> eller <code>http:/<wbr>/<wbr>loinc.<wbr>org|29463-7</code></td></tr>
    <tr><td>Kroppshøyde</td><td><code>http:/<wbr>/<wbr>snomed.<wbr>info/<wbr>sct|50373000</code> eller <code>http:/<wbr>/<wbr>loinc.<wbr>org|8302-2</code></td></tr>
    <tr><td>BMI</td><td><code>http:/<wbr>/<wbr>snomed.<wbr>info/<wbr>sct|60621009</code> eller <code>http:/<wbr>/<wbr>loinc.<wbr>org|39156-5</code></td></tr>
    <tr><td>Blodtrykkspanel</td><td><code>http:/<wbr>/<wbr>loinc.<wbr>org|85354-9</code></td></tr>
    <tr><td>Termin</td><td><code>http:/<wbr>/<wbr>loinc.<wbr>org|11778-8</code></td></tr>
    <tr><td>Fosterets hjertefrekvens</td><td><code>http:/<wbr>/<wbr>snomed.<wbr>info/<wbr>sct|364075005</code> eller <code>http:/<wbr>/<wbr>loinc.<wbr>org|55283-6</code></td></tr>
    <tr><td>Rapporterte fosterbevegelser</td><td><code>http:/<wbr>/<wbr>loinc.<wbr>org|57088-7</code></td></tr>
  </tbody>
</table>

Full feltklassifisering står i [mappingmatrisen](mapping.md).
