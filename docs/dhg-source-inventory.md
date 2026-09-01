# Oversikt over DHG-kilden

Fasaden leser først `/status`, kontrollerer samtykke og aktivt helsekort, og henter deretter `/record/{latestRecordId}`. DHG er eneste datakilde ved kjøring.

| DHG-område | DTO | Eksponert i FHIR |
|---|---:|---|
| `mother` | Ja | Språk, behov for tolk og samboerskap med medforelder. Øvrige demografifelt utelates |
| `currentPregnancy` | Ja | Datoer, antall fostre, assistert befruktning og informasjonsmarkører |
| `previousPregnancies` | Ja | Eksplisitte antall og uparset merknad |
| `geneticDisorders` | Ja | Eksplisitte nullable boolske verdier og uparset merknad |
| `medicalConditions` | Ja | Eksplisitte nullable boolske verdier og uparset merknad |
| `medication` | Ja | Frekvens, merknad, legemiddelallergi og folatopplysninger |
| `lifestyleFactors` | Ja | Kodet stimulustype, frekvens og `dailyCount` |
| `clinicalTests` | Ja | Eksplisitte prøveresultater og uparset merknad |
| `rhesusDNegative` | Ja | Aggregert foster-RhD-resultat, resultatdato, profylakse og merknad. Samtykkefeltet eksponeres ikke |
| `vitalMeasurementsBeforePregnancy` | Ja | Positiv høyde, vekt og BMI uten konstruert måletidspunkt |
| `symphysisFundalHeights` | Ja | Måling og måledato. `pregnancyWeek` mottas, men eksponeres ikke |
| `antenatalAppointments` | Ja | `Encounter`, målinger og fosterfunn. Manglende dato og foster-ID konstrueres ikke |
| `pointsOfContact` | Ja | Fastlege, jordmor, helsestasjon og fødeinstitusjon i `CareTeam` |
| `birthStatus` | Ja | Ikke eksponert |

Ukjente JSON-egenskaper godtas i DTO-ene, men eksponeres ikke automatisk. Egenskapsnavn er skiftsensitive, inkludert `bMI`. Ressurser med `metadata.enteredInError=true` utelates.

Se [mappingmatrisen](mapping.md) for regler på feltnivå.
