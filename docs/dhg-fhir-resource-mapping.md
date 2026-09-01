# Mapping fra DHG API til FHIR R4-ressurser

Dette dokumentet beskriver ressursene som fasaden oppretter. Feltklassifisering står i [mappingmatrisen](mapping.md), og full sporing fra DHG-attributt til FHIR står i [attributtmappingen](dhg-facade-attribute-mapping.md).

## Valg av DHG-post

<table>
  <thead>
    <tr><th width="33.33%" scope="col">DHG-verdi</th><th width="33.33%" scope="col">Kontroll</th><th width="33.33%" scope="col">Feil</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GET /<wbr>status</code></td><td>Henter samtykke, personstatus og <code>latestRecordId</code></td><td><code>OperationOutcome</code></td></tr>
    <tr><td><code>hasGivenConsent</code></td><td>Må være <code>true</code></td><td>403</td></tr>
    <tr><td><code>deceased</code></td><td>Må ikke være <code>true</code></td><td>403</td></tr>
    <tr><td><code>hasActiveMaternityRecord</code></td><td>Må være <code>true</code></td><td>404</td></tr>
    <tr><td><code>latestRecordId</code></td><td>Må være en gyldig, ikke-tom UUID</td><td>503</td></tr>
    <tr><td><code>GET /<wbr>record/<wbr>{latestRecordId}</code></td><td>Eneste kliniske datakilde</td><td>503</td></tr>
    <tr><td><code>metadata.<wbr>recordId</code></td><td>Må samsvare med <code>latestRecordId</code></td><td>503</td></tr>
    <tr><td><code>recordStatus.<wbr>status</code></td><td>Må være <code>ACTIVE</code></td><td>404</td></tr>
  </tbody>
</table>

Fasaden bruker ingen alternativ datakilde og har ikke varig klinisk mellomlagring.

## Ressurser

<table>
  <thead>
    <tr><th width="33.33%" scope="col">Ressurs</th><th width="33.33%" scope="col">Antall</th><th width="33.33%" scope="col">Kilde</th></tr>
  </thead>
  <tbody>
    <tr><td><code>CapabilityStatement</code></td><td>1</td><td>Statisk beskrivelse av FHIR-flaten</td></tr>
    <tr><td><code>Patient</code></td><td>1..*</td><td>Morens logiske ID og eventuelle foster-ID-er fra positiv <code>fosterId</code></td></tr>
    <tr><td><code>Observation</code></td><td>0..*</td><td>Eksplisitte DHG-felt med sikker betydning</td></tr>
    <tr><td><code>Encounter</code></td><td>0..*</td><td>Konsultasjoner som ikke er markert som feil</td></tr>
    <tr><td><code>CareTeam</code></td><td>0..1</td><td>Fastlege, jordmor, helsestasjon og fødeinstitusjon fra <code>pointsOfContact</code></td></tr>
    <tr><td><code>Bundle</code></td><td>1</td><td>Resultat fra FHIR-søk</td></tr>
    <tr><td><code>OperationOutcome</code></td><td>0..1</td><td>Kontrollert feil</td></tr>
  </tbody>
</table>

## Felles regler

- `metadata.enteredInError=true` utelater hele ressursen.
- `null` gir ingen FHIR-verdi. Eksplisitt `false` beholdes.
- `metadata.lastUpdated` mappes til `meta.lastUpdated`.
- Måledato mappes til `effectiveDateTime` med dagspresisjon.
- Observasjoner bruker mor som `subject`.
- Fosterobservasjoner bruker `focus` bare når en positiv `fosterId` finnes.
- Konsultasjonsbaserte observasjoner refererer til kildens `Encounter`, også når dato mangler.
- `Observation` og `Encounter` får status `unknown` fordi DHG ikke leverer en entydig FHIR-status.
- Kliniske koder kommer fra SNOMED CT, NLK, Volven eller LOINC. Måleenheter bruker UCUM.
- Ukjente kodeverk, enumverdier og fritekst oversettes ikke automatisk.
- Alle observasjoner bruker basisressursen FHIR R4 `Observation` uten egne `meta.profile`-verdier.

## Patient og foster

Morens `Patient.id` kommer fra pasientkonteksten eller HMAC-pseudonymiseringen. Den er aldri et fødselsnummer. Navn og bostedsadresse fra `mother` mappes til `Patient.name` og `Patient.address`. Fødeland fra Volven 9043 bruker HL7-utvidelsen `patient-birthPlace`. `mother.language` mappes til `Patient.communication.language`, og `mother.needsLanguageInterpreter` bruker HL7-utvidelsen `patient-interpreterRequired`.

Yrkesaktivitet siste seks måneder, stillingsprosent fra 0 til 100 og uparset tekst for yrke og bransje mappes til `Observation` med kategorien `social-history`.

En positiv `fetusesVitalSigns[].fosterId` gir en pseudonym foster-ID basert på morens logiske ID, aktiv DHG-post og `fosterId`. Fosterressursen inneholder bare `id` og eventuell `meta.lastUpdated`.

## Fødselsstatus

Hver `birthStatus.birthStatus[]` med Volven 8522-status eller eksplisitt leveringstid gir en `Observation` med kategorien `social-history`. Status blir `valueCodeableConcept`, og tidspunktet blir `effectiveDateTime`. En positiv `fosterId` oppretter eller gjenbruker samme pseudonyme fosterressurs som konsultasjonsfunn og settes som `Observation.focus`. Uten positiv ID beholdes opplysningen med mor som `subject` og uten `focus`.

## CareTeam

Fastlege og jordmor mappes til inneholdte `Practitioner`, `Organization` og `PractitionerRole`. HPR-nummer og fastlegens organisasjonsnummer publiseres bare når DHG leverer dem. Helsestasjon og fødeinstitusjon mappes til inneholdte `Organization`-ressurser uten konstruerte identifikatorer.

## Søk

GET-søk bruker logisk pasient-ID og `X-Patient-Context`. Observation støtter filtrene `code`, `category` og dagspresis `date`.

POST `_search` mottar fødselsnummer i forespørselskroppen. I autentisert drift kreves HelseID og pseudonym `Patient.id` fra HMAC. Lokal `DevelopmentTestMode` bruker et konfigurert testalias. Pasientvalget endrer ikke den kliniske mappingen.

Et søk uten treff returnerer en `searchset`-`Bundle` med `total=0`.
