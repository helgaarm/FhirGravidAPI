# Drift og feilhåndtering

## API-konfigurasjon

<table>
  <thead>
    <tr><th width="50%" scope="col">Område</th><th width="50%" scope="col">Nøkler</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Dhg</code></td><td><code>Environment</code>, <code>BaseUrl</code>, <code>SourceSystem</code>, tidsgrenser og antall nye forsøk</td></tr>
    <tr><td><code>HelseId</code></td><td><code>Authority</code>, målgrupper, tilgangsomfang, klient-ID og private JWK-er</td></tr>
    <tr><td><code>HelseIdTestToken</code></td><td>Autentiseringsnøkkel og syntetisk HPR-, rolle- og organisasjonskontekst for DHG Test</td></tr>
    <tr><td><code>AuthGateway</code></td><td><code>SharedSecret</code>, minst 32 byte og lik <code>AUTH_GATEWAY_SHARED_SECRET</code></td></tr>
    <tr><td><code>PatientContext</code></td><td>Headernavn, levetid, <code>PatientIdHmacKey</code> og testaliaser</td></tr>
    <tr><td><code>DevelopmentTestMode</code></td><td>Lokal anonym testmodus og fast testsubjekt</td></tr>
    <tr><td><code>Swagger</code></td><td><code>EnabledInProduction</code>; standardverdien er <code>false</code></td></tr>
    <tr><td><code>ReverseProxy</code></td><td><code>ForwardedHeadersEnabled</code></td></tr>
    <tr><td>OpenTelemetry</td><td>Standardvariabler med prefikset <code>OTEL_</code></td></tr>
  </tbody>
</table>

Oppstart avvises ved manglende sikkerhetskonfigurasjon, ukjent `Dhg:Environment` eller blanding av test- og produksjonsendepunkter. Gyldige DHG-miljøer er `Test` og `Production`.

Utenfor `DevelopmentTestMode` kreves en Base64-kodet `PatientContext:PatientIdHmacKey` på minst 32 byte. `DevelopmentTestMode:Enabled=true` krever `Development`, DHG Test, en loopback-lytter og en kjent loopback-motpart. `HelseIdTestToken:Enabled=true` krever i tillegg en `.test.nhn.no`-URL, autentiseringsnøkkel, klient-ID og en fullstendig syntetisk testidentitet.

## Auth-gateway

Gatewayen konfigureres med miljøvariabler:

<table>
  <thead>
    <tr><th width="50%" scope="col">Variabel</th><th width="50%" scope="col">Implementert betydning</th></tr>
  </thead>
  <tbody>
    <tr><td><code>AUTH_GATEWAY_LISTEN_ADDR</code></td><td>HTTP-lytter; standard <code>:8080</code></td></tr>
    <tr><td><code>AUTH_GATEWAY_UPSTREAM_URL</code></td><td>HTTP-loopback uten sti, spørring eller legitimasjon; standard <code>http:/<wbr>/<wbr>127.<wbr>0.<wbr>0.<wbr>1:8081</code></td></tr>
    <tr><td><code>AUTH_GATEWAY_MODE</code></td><td><code>authenticate</code> validerer HelseID og DPoP. <code>passthrough</code> videresender uten autentisering og har ingen miljøsperre</td></tr>
    <tr><td><code>AUTH_GATEWAY_EXTERNAL_SCHEME</code></td><td>Må være <code>https</code> i autentisert modus; aktiverer ikke TLS</td></tr>
    <tr><td><code>AUTH_GATEWAY_EXTERNAL_HOST</code></td><td>Offentlig vertsnavn som må samsvare med innkommende <code>Host</code></td></tr>
    <tr><td><code>AUTH_GATEWAY_SHARED_SECRET</code></td><td>Intern hemmelighet på minst 32 byte</td></tr>
    <tr><td><code>AUTH_GATEWAY_REPLAY_STORE</code></td><td><code>memory</code> i det dokumenterte enkeltprosessoppsettet</td></tr>
    <tr><td><code>AUTH_GATEWAY_SINGLE_REPLICA</code></td><td><code>true</code> i det dokumenterte enkeltprosessoppsettet</td></tr>
    <tr><td><code>HELSEID_AUTHORITY</code></td><td>HelseID-URL med HTTPS uten sti, spørring eller legitimasjon</td></tr>
    <tr><td><code>HELSEID_AUDIENCE</code> / <code>HELSEID_SCOPE</code></td><td>Fasadens målgruppe og påkrevde lesetilgang</td></tr>
  </tbody>
</table>

Gatewayen terminerer ikke TLS. Den konfigurerte oppstrømsadressen må være loopback. Klientstyrte proxyheadere og den interne gatewayheaderen fjernes før videresending.

Bare `GET`, `HEAD` og `POST` godtas. Forespørselskroppen er begrenset til 1 MiB. Lese- og skrivetidsgrensene er henholdsvis 15 og 60 sekunder.

## Endepunkttilgang

<table>
  <thead>
    <tr><th width="25%" scope="col">Tilgangsvei</th><th width="25%" scope="col">Metadata</th><th width="25%" scope="col">FHIR-data</th><th width="25%" scope="col">Swagger/OpenAPI</th></tr>
  </thead>
  <tbody>
    <tr><td>Gateway i <code>authenticate</code>-modus</td><td>HelseID og DPoP</td><td>HelseID og DPoP</td><td>HelseID og DPoP</td></tr>
    <tr><td>Direkte API i lokal <code>DevelopmentTestMode</code></td><td>Anonym</td><td>Anonym</td><td>Anonym</td></tr>
    <tr><td>Direkte API uten testmodus</td><td>Anonym</td><td>Krever gatewayvalidert HelseID-token</td><td>Anonymt utenfor produksjon; deaktivert som standard i produksjon</td></tr>
  </tbody>
</table>

Gatewayens `/health/live` og `/health/ready` er anonyme. `/health/live` kontrollerer bare gatewayprosessen. `/health/ready` videresender API-ets readiness-sjekk. API-ets readiness-sjekk kontrollerer prosessen, men ikke HelseID eller DHG.

## Telemetri

Egne spor omfatter HelseID-tokenutveksling, HelseID TEST-tokenkall og DHG-kall. DHG-spor bruker den normaliserte verdien `dhg.operation=status|record`. Målingene er `dhg.request.duration` og `dhg.request.errors`.

Token, DPoP-bevis, autentiseringsnøkkel og DHG-svar legges ikke i egendefinerte spor. Generisk HTTP-sporing mot DHG er filtrert bort for å unngå dynamisk post-ID i URL-attributter.

## Feiloversettelse

<table>
  <thead>
    <tr><th width="33.33%" scope="col">Hendelse</th><th width="33.33%" scope="col">HTTP</th><th width="33.33%" scope="col">FHIR <code>issue.<wbr>code</code></th></tr>
  </thead>
  <tbody>
    <tr><td>Ugyldig eller manglende pasientkontekst</td><td>400</td><td><code>invalid</code></td></tr>
    <tr><td>Ugyldig POST-skjema eller fødselsnummerformat</td><td>400</td><td><code>invalid</code></td></tr>
    <tr><td>Manglende eller ugyldig token</td><td>401</td><td><code>security</code></td></tr>
    <tr><td>Manglende samtykke eller forbudt tilgang</td><td>403</td><td><code>forbidden</code></td></tr>
    <tr><td>Ukjent pasient eller manglende aktivt helsekort</td><td>404</td><td><code>not-found</code></td></tr>
    <tr><td>DHG-begrensning</td><td>429</td><td><code>throttled</code></td></tr>
    <tr><td>Metode utenfor <code>GET</code>, <code>HEAD</code> og <code>POST</code></td><td>405</td><td><code>not-supported</code></td></tr>
    <tr><td>Forespørselskropp over 1 MiB</td><td>413</td><td><code>too-costly</code></td></tr>
    <tr><td>Gatewayen får ikke kontakt med API-et</td><td>502</td><td><code>exception</code></td></tr>
    <tr><td>Konfigurasjonsfeil</td><td>500</td><td><code>exception</code></td></tr>
    <tr><td>HelseID- eller DHG-feil</td><td>503</td><td><code>transient</code> eller <code>processing</code></td></tr>
  </tbody>
</table>

Rå feildetaljer fra HelseID og DHG returneres ikke. En uventet 500-feil inneholder en korrelasjons-ID.
