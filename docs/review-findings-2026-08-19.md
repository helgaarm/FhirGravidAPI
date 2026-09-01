# Arkivert gjennomgang 2026-08-19

Dette er en historisk rapport og beskriver ikke gjeldende funksjonalitet eller testantall.

Gjennomgangen førte til disse implementerte endringene:

- Dynamiske DHG-post-ID-er ble fjernet fra egendefinert telemetri.
- Pasientkonteksten ble bundet til autentisert HelseID-subjekt.
- Ukjente DHG-miljønavn og tomt tilgangsomfang ble avvist ved oppstart.
- Nye forsøk ved tidsavbrudd og begge formatene av `Retry-After` ble lagt til.
- Nullable boolske verdier beholdt skillet mellom `true`, `false` og manglende verdi.
- Feilsvar med 401 og 403 ble standardisert som FHIR `OperationOutcome`.
- Tester for aktivt helsekort, samtykke, personvern, HTTP-feilhåndtering, mapping, konfigurasjon, autorisasjon og gjenbruk av bevis ble lagt til.

På gjennomgangstidspunktet bestod 35 tester: 3 kontraktstester, 7 integrasjonstester og 25 enhetstester. Se dagens testkjøring for gjeldende antall.
