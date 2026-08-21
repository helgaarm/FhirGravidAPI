# Sikkerhet

## Tillitsgrenser

FHIR-klienten autentiseres med et HelseID access-token og DPoP-bevis mot auth-gatewayen. Gatewayen validerer issuer, audience, levetid, fasadescope og DPoP sender-constraining før forespørselen sendes til det private API-et. Access-tokenet brukes som `subject_token` i HelseID token exchange. Det nye DPoP-bundne tokenet har DHG audience `nhn:maternity-record` og scope `nhn:maternity-record/api`.

Gatewayen bruker `golang-jwt`, `keyfunc` og HelseIDs anbefalte `AxisCommunications/go-dpop`-bibliotek. Den håndhever `DPoP`-skjema, `at+jwt`, offentlig asymmetrisk JWK uten privat nøkkelmateriale, signatur og algoritme, `htm`/`htu`, 10-sekunders `iat`-vindu, unik `jti`, `ath` og bindingen mot access-tokenets `cnf.jkt`. Det private API-et validerer JWT-et på nytt med Microsofts JWT bearer-handler og krever en delt gateway-hemmelighet som sammenlignes i konstant tid. Innkommende kopier av den interne headeren fjernes alltid av gatewayen.

Replay-registeret kan være atomisk Redis med kort TTL eller prosesslokalt minne. Redis er obligatorisk ved flere replikaer. Minnevarianten nekter å starte med mindre `AUTH_GATEWAY_SINGLE_REPLICA=true` er satt eksplisitt. Replay-nøkler hashes og inneholder verken access-token eller kliniske data.

Unntak: eksplisitt `DevelopmentTestMode` gjør Swagger/FHIR-flaten anonym og bruker normalt server-side HelseID `client_credentials` for DHG Test. En separat `HelseIdTestToken:Enabled`-bryter kan i denne modusen bruke HelseID TEST-tokenverktøyet med en server-side auth key, slik smartOppgave gjør. Verktøyet kalles på nytt for hvert DHG-kall og returnerer et access-token og DPoP-bevis bundet til eksakt metode og URL; verdiene caches, persisteres og logges aldri. Den normale Development-varianten krever loopback-only listener og kjent loopback-peer, må ikke ligge bak proxy/tunnel/port-forwarding og er deaktivert som standard. Repositoryets Azure Test-mal er et særskilt Staging-unntak med eksplisitt `AllowRemoteStaging=true` og obligatorisk CIDR-begrensning i Container Apps. Begge variantene feiler mot annet enn DHG Test og endrer ikke DHGs krav til HelseID, DPoP, audience eller scope.

Swagger/OpenAPI er deaktivert som standard når enten host-miljøet eller DHG-miljøet er Production. Hvis `Swagger:EnabledInProduction=true`, håndheves den ordinære HelseID `population.read`-policyen for Swagger UI, Swashbuckle-dokumentet og ASP.NET OpenAPI-dokumentet. Anonyme kall får `401`, og autentiserte kall uten korrekt fasadescope får `403`. Fordi HelseID krever DPoP og webklientintegrasjon i backend, krever praktisk bruk av produksjons-UI en godkjent HelseID-aware backend/reverse proxy; token skal ikke eksponeres eller limes inn i nettleseren.

HelseID-kall bruker `private_key_jwt` med:

- `typ=client-authentication+jwt`
- `iss` og `sub` lik klient-ID
- audience lik HelseID authority, ikke token-endepunktet
- unik `jti`
- maksimalt åtte sekunders levetid
- nytt assertion ved nonce-retry

DPoP-nøkler og assertion-nøkler er separate driftshemmeligheter. Nøkkelrotasjon må koordineres med HelseID-registreringen.

## Pasientkontekst

Fødselsnummer tas aldri fra URL, query eller FHIR-logisk ID. En formålsbundet ASP.NET Data Protection-token inneholder logisk pasient-ID, fødselsnummer, autentisert HelseID-`sub` og utløp. Tokenet sendes i konfigurert pasientkontekst-header; route-/search-ID og innlogget subjekt må matche innholdet. Standard levetid er ti minutter, og svar er merket `Cache-Control: no-store`.

Alias-endepunktet er kun støtte for konfigurerte syntetiske DHG Test-personer og finnes ikke i Production. Det krever normalt samme autorisasjon som FHIR-flaten; i eksplisitt Development-testmodus er det anonymt og binder konteksten til det faste konfigurerte test-subjektet. Det returnerer aldri fødselsnummeret.

Kun på en lokal `DevelopmentTestMode`-host kan en konfigurert syntetisk testperson også velges med FHIR POST `_search`. Fødselsnummeret må ligge i en liten `application/x-www-form-urlencoded` body, må matche nøyaktig én konfigurert alias og returneres aldri. GET-query med fødselsnummer støttes ikke. POST-rutene registreres ikke i remote Staging, QA eller Production, og request body tas ikke inn i applikasjonens logger eller telemetry.

Det finnes foreløpig ingen godkjent produksjonsutsteder for pasientkontekst. Produksjon skal ikke åpnes før tillitsprotokoll, autorisasjonsgrunnlag, nøkkelstyring, rotasjon og replay-kontroll er arkitekturgodkjent og testet.

For flere instanser må Data Protection-nøkkelringen lagres i en godkjent, delt og kryptert nøkkeltjeneste. Standard lokal nøkkelring er bare egnet for lokal utvikling eller én instans.

## Hemmeligheter og logging

Følgende skal aldri ligge i kildekode, container-image, telemetry eller logger:

- access-/refresh-token og DPoP proof
- HelseID TEST-tokenverktøyets auth key
- private JWK-er
- fødselsnummer
- DHG response body eller klinisk FHIR payload

Logger og egne spans bruker bare feilklasse, status, destinasjonens host, normalisert operasjon (`status`/`record`), retrynummer og tilfeldig korrelasjons-ID. Generisk HTTP-tracing er deaktivert for DHG-verten for å hindre at dynamisk record-ID kommer inn i URL-attributter. Korrelasjons-ID fra klient aksepteres bare hvis den er en GUID.

## Nettverk

Alle eksterne URL-er må være HTTPS. Test og produksjon valideres som sammenhørende miljø. Utgående nettverk bør begrenses til valgt HelseID authority, DHG-host og OTLP-endepunkt. TLS-terminering/proxy må bevare original scheme/host på en kontrollert måte dersom absolutte Bundle-URL-er skal være korrekte.

## Produksjonssjekkliste

- registrer korrekt facade audience/scope og token-exchange-relasjon i HelseID
- lagre og roter JWK-er i HSM/Key Vault eller tilsvarende
- konfigurer persistent kryptert Data Protection key ring
- bruk Redis replay-register med TLS før flere replikaer tas i bruk; minnevarianten er kun tillatt ved eksplisitt én-replika-drift
- eksponer bare gateway-porten, hold API-port 8081 privat, og roter den delte gateway-hemmeligheten som en driftshemmelighet
- fjern alle testaliaser og sett `ASPNETCORE_ENVIRONMENT=Production`
- behold Swagger/OpenAPI deaktivert i Production, eller dokumenter behovet, aktiver eksplisitt og verifiser HelseID-beskyttelsen
- implementer og godkjenn produksjonsutsteder for subjektbundet pasientkontekst
- verifiser klokkesynkronisering, egress, sertifikatkjede og OTLP-redigering
- kjør penetrasjonstest og personvern-/risikovurdering før klinisk bruk
