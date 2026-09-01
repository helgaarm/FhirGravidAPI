# DHG → FHIR-mappingmatrise

Full sporing fra DHG JSON-sti via fasademodellen til FHIR står i [attributtmappingen](dhg-facade-attribute-mapping.md). Ressursformene står i [ressursmappingen](dhg-fhir-resource-mapping.md).

Klassifisering:

- **DIRECT**: Kildeverdien representeres med samme betydning i FHIR.
- **PARTIAL**: Bare den delen som har sikker betydning, eksponeres.
- **UNSUPPORTED**: Feltet mottas, men eksponeres ikke.

`metadata.enteredInError=true` utelater hele ressursen. `null` gir ingen observasjon. Eksplisitt `false` beholdes.

| DHG-område | Status | Implementert mapping |
|---|---|---|
| Aktiv post og metadata | DIRECT | Post-ID og `ACTIVE` kontrolleres. Kildens tidsstempel blir `meta.lastUpdated` |
| `mother.language` | DIRECT | `Patient.communication.language` med Volven 3303 |
| `mother.needsLanguageInterpreter` | DIRECT | HL7-utvidelsen `patient-interpreterRequired` |
| `mother.cohabitingCoparent` og merknad | PARTIAL | Boolsk observasjon og uparset tekst uten utledning av relasjon eller husstand |
| Øvrige `mother`-felt | UNSUPPORTED | Demografi, arbeid og kontaktdata eksponeres ikke |
| Termindatoer og siste menstruasjon | DIRECT/PARTIAL | Eksplisitte datoer eksponeres. Korrigert termin beholdes separat uten prioritering |
| `numberOfFetuses` | PARTIAL | Positivt heltall med SNOMED CT `246435002` |
| Assistert befruktning | DIRECT | Boolsk verdi med SNOMED CT `813541000000100`; dato brukes bare når status er `true` |
| Tidligere svangerskap | DIRECT/PARTIAL | Eksplisitte antall eksponeres. Merknad beholdes uten residualberegning |
| Arvelige sykdommer | DIRECT/PARTIAL | Eksplisitte boolske verdier og uparset merknad. Ingen sykdomstype eller berørt person utledes |
| Medisinske tilstander | DIRECT/PARTIAL | Eksplisitte boolske verdier og uparset merknad. Sammensatte felt splittes ikke |
| Legemiddelopplysninger | DIRECT/PARTIAL | Allergi og folatstatus eksponeres. Frekvens og merknad tolkes ikke som legemiddel eller dose |
| Livsstil | DIRECT/PARTIAL | Volven 8536/8537 og ikke-negativ `dailyCount` uten konstruert enhet |
| Kliniske prøver | DIRECT/PARTIAL | Verifiserte NLK-, LOINC- og SNOMED CT-koder brukes. Andre uttrykkelige resultater bruker presis tekst |
| Foster-RhD | DIRECT/PARTIAL | Aggregert resultat, resultatdato, profylakse og merknad. Samtykke eksponeres ikke |
| Høyde, vekt og BMI før svangerskapet | PARTIAL | SNOMED CT/LOINC og UCUM uten konstruert måletidspunkt |
| Symfyse-fundus-mål | DIRECT | Positiv måling og måledato. `pregnancyWeek` eksponeres ikke |
| Konsultasjon | DIRECT/PARTIAL | `Encounter` og konsultasjonsbaserte observasjoner. Manglende dato gir ingen `period` eller `effective[x]` |
| Svangerskapsalder | DIRECT | LOINC `18185-9`, UCUM `d`, én observasjon per konsultasjon |
| Blodtrykk | PARTIAL | LOINC `85354-9` med systolisk og diastolisk komponent når verdien kan tolkes sikkert |
| Ødem | PARTIAL | Heltall fra 0 til 3 uten navngitte skalatrinn |
| Positiv `fosterId` | DIRECT | Minimal, pseudonym fosterressurs som brukes i `Observation.focus` |
| Fosterfunn uten positiv `fosterId` | PARTIAL | Funnet beholdes med mor som `subject`, uten `focus` |
| Fastlege og jordmor | DIRECT | Inneholdte `Practitioner`, `Organization` og `PractitionerRole`; kildens HPR- og organisasjonsnummer beholdes |
| Helsestasjon og fødeinstitusjon | DIRECT | Inneholdt `Organization` uten konstruert identifikator |
| `birthStatus` og `lastUpdatedBy` | UNSUPPORTED | Eksponeres ikke |

## Terminologiregler

- Fasaden publiserer ingen egne kliniske koder.
- NLK og Volven brukes bare ved dokumentert, entydig mapping.
- LOINC brukes for interoperabilitet. SNOMED CT brukes ved entydig klinisk betydning.
- UCUM brukes for måleenheter.
- Ukjente kodeverk, enumverdier og fritekst oversettes ikke automatisk.
- Positive tallkrav fra DHG håndheves som kontraktvalidering, ikke som kliniske referanseområder.
