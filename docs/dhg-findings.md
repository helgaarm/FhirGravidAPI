# DHG-funn og beslutninger

Gjennomgangsdato: 2026-08-23.

Denne loggen skiller verifiserte implementeringsbeslutninger fra uavklarte kliniske spørsmål og spørsmål om ekstern integrasjon.

## Verifiserte beslutninger

- Begge DHG source-feltene for hemoglobin refererer til NLK `NOR05172` og UCUM `g/dL`. Third-trimester context beholdes som annotation; fasaden lager ikke en ny clinical code for trimesteret.
- DHG angir NLK `NPU19763` for både P- og S-ferritin. Dette er i samsvar med NLK-veilederen, som beskriver serum som sample processing og koder resultatet mot patientens plasma; fasaden trenger derfor ikke å gjette specimen.
- ABO og RhD bruker NLK `NPU58582` og `NPU21917`. LOINC `883-9` og `10331-7` beholdes som tilleggskoding for interoperability.
- Bare HBV surface antigen har tilstrekkelig source precision for en coded positive/negative infection Observation. Generiske eller kontraktuelt uverifiserte booleans som HIV, syphilis, Chlamydia, Rubella, Hepatitis C, asymptomatic bacteriuria og GBS eksponeres ikke med assay-spesifikke koder.
- Hver datert appointment beholder én gestational-age Observation med LOINC `18185-9` og exact total days som UCUM `d`. Consumer velger nyeste `effectiveDateTime`; fasaden lager ikke en duplicate «latest» fact.
- Consent og fetal RhD result eksponeres ikke som mother-subject Observations. Bare den entydige antenatal anti-D prophylaxis status mappes i gjeldende resource set.
- Nullable booleans beholder alle tre tilstander: true, false og fraværende.
- Facade-owned clinical codes er fjernet. LOINC, SNOMED CT, NLK, Volven og UCUM brukes bare ved exact semantic match; HL7 core extension brukes for interpreter requirement.
- BMI er en UCUM quantity (`kg/m2`), ikke et unitless decimal. Blood pressure er en component-only Observation uten en konkurrerende top-level text value.
- `dueDateCorrectedDate` eksponeres ikke uten en eksplisitt clinical precedence decision. IVF-dato brukes bare som `effectiveDateTime` når explicit IVF-status er true; dato alene utleder aldri status.
- FHIR kalenderdatoer serialiseres som `valueDateTime`/`effectiveDateTime` med day precision, fordi `date` ikke er tillatt i disse FHIR R4 choice-elementene.
- Fetus-spesifikke facts og edema grade er flyttet til unsupported. Førstnevnte krever strukturert fetus `focus`; sistnevnte mangler autoritativ scale semantics.
- DHG positivity/range constraints håndheves før FHIR mapping uten å introdusere egne clinical reference ranges.
- Bare Body Weight deklarerer norsk Vital Signs canonical. Instansen valideres i CI mot pinned `hl7.fhir.no.domain.vitalsigns#0.9.74`/NoBasis `2.2.2`. Blood Pressure beholder entydige codings, men ikke canonical claim før draft-profilens slicing kan valideres.

## Åpne funn / release gates

- Clinical terminology owner må godkjenne alle codes, units, datatypes og FHIR category/status før ekstern promotering til DHG Test.
- Pre-pregnancy height, weight og BMI mangler source measurement time. Height/weight har norske SNOMED CT-codings, profile-required LOINC og korrekte UCUM value types, men kan ikke erklære full conformance til FHIR R4 Vital Signs profile før temporal context er avklart.
- Faktiske DHG Test `/status`- og `/record`-payloads er ikke verifisert med en opt-in end-to-end test.
- HelseID discovery, token exchange, DPoP nonce-håndtering og DHG resource calls er ikke kjørt med godkjente eksterne credentials.
- Det finnes ingen godkjent production patient-context issuer/trust protocol.
- QA er ikke en støttet `Dhg:Environment`-verdi før eksakte endpoints og validation rules er godkjent.

## Kontrollerte autoritative referanser

- [NHN DHG resource model](https://utviklerportal.nhn.no/informasjonstjenester/digitalt-helsekort-for-gravide/digitalt-helsekort-for-gravide-api/hit-maternity-record-api/docs/api/resourcesmd/)
- [NHN laboratory message example showing NOR05172 with g/dL](https://utviklerportal.nhn.no/no/informasjonstjenester/kjernejournal/pasientens-proevesvar/pps-documentation/docs/svarmeldingmd)
- [HL7 FHIR R4 Vital Signs profile](https://hl7.org/fhir/R4/observation-vitalsigns.html)
- [Helsedirektoratet om SNOMED CT](https://www.helsedirektoratet.no/digitalisering-og-e-helse/snomed-ct)
- [HL7 Patient extension: interpreter required](https://www.hl7.org/fhir/R4/patient-extensions.html)
- [LOINC 8665-2 Last menstrual period start date](https://loinc.org/8665-2)
- [LOINC 11778-8 Delivery date Estimated](https://loinc.org/11778-8)
- [LOINC 39156-5 Body mass index](https://loinc.org/39156-5)
- [LOINC 85354-9 Blood pressure panel](https://loinc.org/85354-9)
- [LOINC 55283-6 Fetal Heart rate](https://loinc.org/55283-6)

Kontroller disse kildene på nytt og registrer datoen hver gang terminologi eller DHG contract endres.
