# Oversikt over DHG-kilden

Fasaden leser først `/status`, kontrollerer samtykke og aktivt helsekort, og henter deretter `/record/{latestRecordId}`. DHG er eneste datakilde ved kjøring.

<table>
  <thead>
    <tr><th width="33.33%" scope="col">DHG-område</th><th width="33.33%" scope="col">DTO</th><th width="33.33%" scope="col">Eksponert i FHIR</th></tr>
  </thead>
  <tbody>
    <tr><td><code>mother</code></td><td>Ja</td><td>Navn, adresse, fødeland, språk, behov for tolk, arbeidsopplysninger og samboerskap med medforelder</td></tr>
    <tr><td><code>currentPregnancy</code></td><td>Ja</td><td>Datoer, antall fostre, assistert befruktning og informasjonsmarkører</td></tr>
    <tr><td><code>previousPregnancies</code></td><td>Ja</td><td>Eksplisitte antall og uparset merknad</td></tr>
    <tr><td><code>geneticDisorders</code></td><td>Ja</td><td>Eksplisitte nullable boolske verdier og uparset merknad</td></tr>
    <tr><td><code>medicalConditions</code></td><td>Ja</td><td>Eksplisitte nullable boolske verdier og uparset merknad</td></tr>
    <tr><td><code>medication</code></td><td>Ja</td><td>Frekvens, merknad, legemiddelallergi og folatopplysninger</td></tr>
    <tr><td><code>lifestyleFactors</code></td><td>Ja</td><td>Kodet stimulustype, frekvens og <code>dailyCount</code></td></tr>
    <tr><td><code>clinicalTests</code></td><td>Ja</td><td>Eksplisitte prøveresultater og uparset merknad</td></tr>
    <tr><td><code>rhesusDNegative</code></td><td>Ja</td><td>Aggregert foster-RhD-resultat, resultatdato, profylakse og merknad. Samtykkefeltet eksponeres ikke</td></tr>
    <tr><td><code>vitalMeasurementsBeforePregnancy</code></td><td>Ja</td><td>Positiv høyde, vekt og BMI uten konstruert måletidspunkt</td></tr>
    <tr><td><code>symphysisFundalHeights</code></td><td>Ja</td><td>Måling og måledato. <code>pregnancyWeek</code> mottas, men eksponeres ikke</td></tr>
    <tr><td><code>antenatalAppointments</code></td><td>Ja</td><td><code>Encounter</code>, målinger og fosterfunn. Manglende dato og foster-ID konstrueres ikke</td></tr>
    <tr><td><code>pointsOfContact</code></td><td>Ja</td><td>Fastlege, jordmor, helsestasjon og fødeinstitusjon i <code>CareTeam</code></td></tr>
    <tr><td><code>birthStatus</code></td><td>Ja</td><td>Kodet fødselsstatus, leveringstid og eventuell positiv foster-ID</td></tr>
  </tbody>
</table>

Ukjente JSON-egenskaper godtas i DTO-ene, men eksponeres ikke automatisk. Egenskapsnavn er skiftsensitive, inkludert `bMI`. Ressurser med `metadata.enteredInError=true` utelates.

Se [mappingmatrisen](mapping.md) for regler på feltnivå.
