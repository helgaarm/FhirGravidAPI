# DHG → FHIR-mappingmatrise

Full sporing fra DHG JSON-sti via fasademodellen til FHIR står i [attributtmappingen](dhg-facade-attribute-mapping.md). Ressursformene står i [ressursmappingen](dhg-fhir-resource-mapping.md).

Klassifisering:

- **DIRECT**: Kildeverdien representeres med samme betydning i FHIR.
- **PARTIAL**: Bare den delen som har sikker betydning, eksponeres.
- **UNSUPPORTED**: Feltet mottas, men eksponeres ikke.

`metadata.enteredInError=true` utelater hele ressursen. `null` gir ingen observasjon. Eksplisitt `false` beholdes.

<table>
  <thead>
    <tr><th width="33.33%" scope="col">DHG-område</th><th width="33.33%" scope="col">Status</th><th width="33.33%" scope="col">Implementert mapping</th></tr>
  </thead>
  <tbody>
    <tr><td>Aktiv post og metadata</td><td>DIRECT</td><td>Post-ID og <code>ACTIVE</code> kontrolleres. Kildens tidsstempel blir <code>meta.<wbr>lastUpdated</code></td></tr>
    <tr><td><code>mother.<wbr>name</code> og adressefelter</td><td>DIRECT</td><td><code>Patient.<wbr>name.<wbr>text</code> og <code>Patient.<wbr>address</code>; navnet deles ikke i fornavn og etternavn</td></tr>
    <tr><td><code>mother.<wbr>countryOfBirth</code></td><td>PARTIAL</td><td>HL7-utvidelsen <code>patient-birthPlace</code>; bare Volven 9043 godtas</td></tr>
    <tr><td><code>mother.<wbr>language</code></td><td>DIRECT</td><td><code>Patient.<wbr>communication.<wbr>language</code> med Volven 3303</td></tr>
    <tr><td><code>mother.<wbr>needsLanguageInterpreter</code></td><td>DIRECT</td><td>HL7-utvidelsen <code>patient-interpreterRequired</code></td></tr>
    <tr><td><code>mother</code>-arbeidsopplysninger</td><td>DIRECT/PARTIAL</td><td>Boolsk yrkesstatus, UCUM-prosent fra 0 til 100 og uparset tekst som <code>social-history</code>-observasjoner</td></tr>
    <tr><td><code>mother.<wbr>cohabitingCoparent</code> og merknad</td><td>PARTIAL</td><td>Boolsk observasjon og uparset tekst uten utledning av relasjon eller husstand</td></tr>
    <tr><td>Termindatoer og siste menstruasjon</td><td>DIRECT/PARTIAL</td><td>Eksplisitte datoer eksponeres. Korrigert termin beholdes separat uten prioritering</td></tr>
    <tr><td><code>numberOfFetuses</code></td><td>PARTIAL</td><td>Positivt heltall med SNOMED CT <code>246435002</code></td></tr>
    <tr><td>Assistert befruktning</td><td>DIRECT</td><td>Boolsk verdi med SNOMED CT <code>813541000000100</code>; dato brukes bare når status er <code>true</code></td></tr>
    <tr><td>Tidligere svangerskap</td><td>DIRECT/PARTIAL</td><td>Eksplisitte antall eksponeres. Merknad beholdes uten residualberegning</td></tr>
    <tr><td>Arvelige sykdommer</td><td>DIRECT/PARTIAL</td><td>Eksplisitte boolske verdier og uparset merknad. Ingen sykdomstype eller berørt person utledes</td></tr>
    <tr><td>Medisinske tilstander</td><td>DIRECT/PARTIAL</td><td>Eksplisitte boolske verdier og uparset merknad. Sammensatte felt splittes ikke</td></tr>
    <tr><td>Legemiddelopplysninger</td><td>DIRECT/PARTIAL</td><td>Allergi og folatstatus eksponeres. Frekvens og merknad tolkes ikke som legemiddel eller dose</td></tr>
    <tr><td>Livsstil</td><td>DIRECT/PARTIAL</td><td>Volven 8536/8537 og ikke-negativ <code>dailyCount</code> uten konstruert enhet</td></tr>
    <tr><td>Kliniske prøver</td><td>DIRECT/PARTIAL</td><td>Verifiserte NLK-, LOINC- og SNOMED CT-koder brukes. Andre uttrykkelige resultater bruker presis tekst</td></tr>
    <tr><td>Foster-RhD</td><td>DIRECT/PARTIAL</td><td>Aggregert resultat, resultatdato, profylakse og merknad. Samtykke eksponeres ikke</td></tr>
    <tr><td>Høyde, vekt og BMI før svangerskapet</td><td>PARTIAL</td><td>SNOMED CT/LOINC og UCUM uten konstruert måletidspunkt</td></tr>
    <tr><td>Symfyse-fundus-mål</td><td>DIRECT</td><td>Positiv måling og måledato. <code>pregnancyWeek</code> eksponeres ikke</td></tr>
    <tr><td>Konsultasjon</td><td>DIRECT/PARTIAL</td><td><code>Encounter</code> og konsultasjonsbaserte observasjoner. Manglende dato gir ingen <code>period</code> eller <code>effective[x]</code></td></tr>
    <tr><td>Svangerskapsalder</td><td>DIRECT</td><td>LOINC <code>18185-9</code>, UCUM <code>d</code>, én observasjon per konsultasjon</td></tr>
    <tr><td>Blodtrykk</td><td>PARTIAL</td><td>LOINC <code>85354-9</code> med systolisk og diastolisk komponent når verdien kan tolkes sikkert</td></tr>
    <tr><td>Ødem</td><td>PARTIAL</td><td>Heltall fra 0 til 3 uten navngitte skalatrinn</td></tr>
    <tr><td>Positiv <code>fosterId</code></td><td>DIRECT</td><td>Minimal, pseudonym fosterressurs som brukes i <code>Observation.<wbr>focus</code></td></tr>
    <tr><td>Fosterfunn uten positiv <code>fosterId</code></td><td>PARTIAL</td><td>Funnet beholdes med mor som <code>subject</code>, uten <code>focus</code></td></tr>
    <tr><td>Fastlege og jordmor</td><td>DIRECT</td><td>Inneholdte <code>Practitioner</code>, <code>Organization</code> og <code>PractitionerRole</code>; kildens HPR- og organisasjonsnummer beholdes</td></tr>
    <tr><td>Helsestasjon og fødeinstitusjon</td><td>DIRECT</td><td>Inneholdt <code>Organization</code> uten konstruert identifikator</td></tr>
    <tr><td><code>birthStatus</code></td><td>DIRECT/PARTIAL</td><td>Volven 8522 som kodet verdi, leveringstid som <code>effectiveDateTime</code> og positiv <code>fosterId</code> som <code>focus</code> på en pseudonym fosterressurs</td></tr>
    <tr><td><code>lastUpdatedBy</code></td><td>UNSUPPORTED</td><td>Eksponeres ikke</td></tr>
  </tbody>
</table>

## Terminologiregler

- Fasaden publiserer ingen egne kliniske koder.
- NLK og Volven brukes bare ved dokumentert, entydig mapping.
- LOINC brukes for interoperabilitet. SNOMED CT brukes ved entydig klinisk betydning.
- UCUM brukes for måleenheter.
- Ukjente kodeverk, enumverdier og fritekst oversettes ikke automatisk.
- Positive tallkrav fra DHG håndheves som kontraktvalidering, ikke som kliniske referanseområder.
