# DHG-funn og beslutninger

Gjennomgangsdato: 2026-08-23.

Denne loggen skiller verifiserte implementeringsbeslutninger fra uavklarte kliniske spørsmål og spørsmål om ekstern integrasjon.

## Verifiserte beslutninger

- Begge DHG source-feltene for hemoglobin refererer til NLK `NOR05172` og UCUM `g/dL`. Third-trimester context beholdes som annotation; fasaden lager ikke en ny clinical code for trimesteret.
- DHG angir NLK `NPU19763` for både P- og S-ferritin. Dette er i samsvar med NLK-veilederen, som beskriver serum som sample processing og koder resultatet mot patientens plasma; fasaden trenger derfor ikke å gjette specimen.
- ABO og RhD bruker NLK `NPU58582` og `NPU21917`. LOINC `883-9` og `10331-7` beholdes som tilleggskoding for interoperability.
- HBV surface antigen har tilstrekkelig source precision for en SNOMED-coded positive/negative Observation. DHG definerer også HBV core-antistoff, blood type antibodies, HIV, syphilis, Chlamydia, toxoplasmosis og hepatitis C som test results der `true` er positivt, `false` negativt og `null` ikke tatt. Resultater uten verifisert analyttkode eksponeres med presis DHG-term i `Observation.code.text` og kodeverk 8340 som result, uten å konstruere assay-specific eller facade codes. `rubellaAntigen` mappes etter den autoritative DHG-beskrivelsen til P-Rubellavirus IgG med NLK `NPU12412`; det misvisende JSON-feltnavnet brukes ikke som clinical term.
- Hver datert appointment beholder én gestational-age Observation med LOINC `18185-9` og exact total days som UCUM `d`. Consumer velger nyeste `effectiveDateTime`; fasaden lager ikke en duplicate «latest» fact.
- Consent og fetal RhD result eksponeres ikke som Observations. Fetal RhD-blokken mangler `fosterId` og kan derfor ikke knyttes entydig til en fetus Patient ved flerlinger. Bare den entydige antenatal anti-D prophylaxis status mappes i gjeldende resource set.
- Nullable booleans beholder alle tre tilstander: true, false og fraværende.
- De gule/grønne genetic-disorder-feltene `noneKnown`, `parentsAreRelatives`, `other` og `note` eksponeres som Observations. `parentsAreRelatives` beholder verifisert SNOMED CT coding; de øvrige bruker presis source-term i `Observation.code.text`, og note beholdes uparset som `valueString`.
- Facade-owned clinical codes er fjernet. LOINC, SNOMED CT, NLK, Volven og UCUM brukes bare ved exact semantic match; HL7 core extension brukes for interpreter requirement.
- Blood pressure er en component-only Observation uten en konkurrerende top-level text value.
- `dueDateCorrectedDate` eksponeres ikke uten en eksplisitt clinical precedence decision. Assisted-conception status bruker SNOMED CT `813541000000100`, som FinnKode returnerer med norsk term «svangerskap ved assistert befruktning». Dato brukes bare som `effectiveDateTime` når status eksplisitt er `true`; status og dato utledes aldri fra hverandre.
- FHIR kalenderdatoer serialiseres som `valueDateTime`/`effectiveDateTime` med day precision, fordi `date` ikke er tillatt i disse FHIR R4 choice-elementene.
- Positivt `fosterId` gir en minimal pregnancy-scoped fetus Patient. Fetal heart rate, presentation/lie, maternal report of movements og uparset note bruker mor som `Observation.subject`, fosteret som `Observation.focus` og det daterte appointment som `encounter`. Edema grade er fortsatt unsupported fordi autoritativ scale semantics mangler.
- Maternal report of fetal movements bruker LOINC `57088-7`, som inngår i LOINCs routine prenatal assessment panel. SNOMED CT `268470003 |Fetal movements felt|` legges ikke til fordi det er en positiv finding og ville være semantisk feil som state-neutral code når DHG eksplisitt kan levere `false`.
- DHG positivity/range constraints håndheves før FHIR mapping uten å introdusere egne clinical reference ranges.
- Alle Observations bruker standard FHIR R4 `Observation` base resource. Fasaden deklarerer ingen draft Vital Signs canonical i `meta.profile` eller `CapabilityStatement.supportedProfile`.
- Pre-pregnancy height, weight og BMI eksponeres med standard SNOMED CT/LOINC codings og UCUM units. Fordi DHG ikke leverer measurement time, er `effective[x]` fraværende og resources deklarerer ikke FHIR R4 Vital Signs profile conformance. DHG `metadata.lastUpdated` beholdes bare som `meta.lastUpdated`.
- De markerte `pointsOfContact.midwife`- og `maternityHealthcareCentre`-feltene eksponeres i patient-scoped `CareTeam`. Jordmor og organization er contained resources fordi source-navn alene ikke er directory-identiteter; GP, birth institute og konstruerte identifiers utelates.
- Representative mapper-genererte Patient-, Encounter-, CareTeam- og Observation-varianter valideres i CI mot pinned `hl7.fhir.r4.core#4.0.1`.

## Åpne funn / release gates

- Clinical terminology owner må godkjenne alle codes, units, datatypes og FHIR category/status før ekstern promotering til DHG Test.
- Faktiske DHG Test `/status`- og `/record`-payloads er ikke verifisert med en opt-in end-to-end test.
- HelseID discovery, token exchange, DPoP nonce-håndtering og DHG resource calls er ikke kjørt med godkjente eksterne credentials.
- Det finnes ingen godkjent production patient-context issuer/trust protocol.
- QA er ikke en støttet `Dhg:Environment`-verdi før eksakte endpoints og validation rules er godkjent.

## Kontrollerte autoritative referanser

- [NHN DHG resource model](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/resourcesmd/)
- [NHN laboratory message example showing NOR05172 with g/dL](https://utviklerportal.nhn.no/no/informasjonstjenester/kjernejournal/pasientens-proevesvar/pps-documentation/docs/svarmeldingmd)
- [HL7 FHIR R4 Observation](https://hl7.org/fhir/R4/observation.html)
- [HL7 FHIR R4 fetal Patient example](https://hl7.org/fhir/R4/patient-examples.html)
- [HL7 FHIR R4 CareTeam](https://hl7.org/fhir/R4/careteam.html)
- [HL7 FHIR R4 contained resources](https://hl7.org/fhir/R4/references.html#contained)
- [Helsedirektoratet om SNOMED CT](https://www.helsedirektoratet.no/digitalisering-og-e-helse/snomed-ct)
- [HL7 Patient extension: interpreter required](https://www.hl7.org/fhir/R4/patient-extensions.html)
- [LOINC 8665-2 Last menstrual period start date](https://loinc.org/8665-2)
- [LOINC 11778-8 Delivery date Estimated](https://loinc.org/11778-8)
- [LOINC 39156-5 Body mass index](https://loinc.org/39156-5)
- [LOINC 85354-9 Blood pressure panel](https://loinc.org/85354-9)
- [LOINC 55283-6 Fetal Heart rate](https://loinc.org/55283-6)
- [LOINC 100230-2 Routine prenatal assessment panel](https://loinc.org/100230-2)

Kontroller disse kildene på nytt og registrer datoen hver gang terminologi eller DHG contract endres.
