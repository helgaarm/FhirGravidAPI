# Sikkerhet

## Tillitsgrenser

FHIR-klienten sender et HelseID-tilgangstoken og et DPoP-bevis til `auth-gateway`. Gatewayen validerer utsteder, målgruppe, levetid, tilgangsomfang, DPoP-signatur, `htm`, `htu`, `ath`, `cnf.jkt` og unik `jti`. Gjenbruk av samme DPoP-bevis avvises. Nøkkelen i gjenbruksregisteret er en SHA-256-verdi og inneholder ikke token eller kliniske data.

Gatewayen fjerner innkommende kopier av den interne gatewayheaderen. Det private API-et validerer JWT-et på nytt og sammenligner gatewayens delte hemmelighet i konstant tid.

Det innkommende tilgangstokenet brukes som `subject_token` i HelseID-tokenutvekslingen. Det utvekslede, DPoP-bundne tokenet har DHG-målgruppen `nhn:maternity-record` og tilgangsomfanget `nhn:maternity-record/api`.

HelseID-kall med `private_key_jwt` bruker:

- `typ=client-authentication+jwt`
- klient-ID som `iss` og `sub`
- HelseID-utstederen som `aud`
- unik `jti`
- maksimalt åtte sekunders levetid
- en ny klientpåstand ved ny forespørsel etter DPoP-nonce

Nøkkelen for klientpåstanden og DPoP-nøkkelen er separate hemmeligheter.

## Lokal utviklingstest

`DevelopmentTestMode` gjør API-ets Swagger- og FHIR-ruter anonyme. Modusen godtas bare i `Development` mot DHG Test, med loopback-lytter og en kjent loopback-motpart.

`HelseIdTestToken:Enabled` henter et nytt token og DPoP-bevis for hvert DHG-kall. `/status` bruker et maskintoken. `/record` bruker et brukertoken med konfigurert syntetisk HPR- og organisasjonskontekst. Token og DPoP-bevis mellomlagres, lagres permanent eller logges ikke.

## Pasientvalg

GET-operasjonene bruker en kortlivet ASP.NET Data Protection-verdi med logisk pasient-ID, fødselsnummer, HelseID-`sub` og utløpstid. Pasient-ID-en i ruten eller søket, innlogget subjekt og innholdet i pasientkonteksten må samsvare. Standard levetid er ti minutter.

`POST /test/patient-context/{alias}` finnes bare utenfor produksjon. Endepunktet bruker konfigurerte syntetiske DHG Test-aliaser og returnerer aldri fødselsnummeret.

FHIR POST `_search` mottar fødselsnummeret i en `application/x-www-form-urlencoded`-kropp og bruker ikke `X-Patient-Context`. I autentisert drift kreves HelseID-tilgangsomfanget `population.read`. Fasaden lager en pseudonym FHIR-ID med `HMAC-SHA-256`. I lokal `DevelopmentTestMode` må fødselsnummeret samsvare med et konfigurert syntetisk alias.

Fødselsnummer i URL støttes ikke. Fødselsnummeret returneres ikke i FHIR, og forespørselskroppen logges ikke av applikasjonen.

Det finnes ingen produksjonsutsteder for `X-Patient-Context` i repositoriet. De kontekstbaserte GET-operasjonene har derfor ingen komplett produksjonsflyt. Den HelseID-beskyttede POST `_search`-flyten er implementert separat.

## Hemmeligheter og logging

Følgende skal ikke ligge i kildekode, containerbilder eller logger:

- tilgangstoken, oppfriskningstoken og DPoP-bevis
- autentiseringsnøkkelen til HelseID TEST-tokenverktøyet
- private JWK-er
- `PatientContext:PatientIdHmacKey`
- fødselsnummer
- DHG-svar og kliniske FHIR-data

Egne DHG-spor inneholder feilklasse, status, vertsnavn, normalisert operasjon (`status` eller `record`), forsøksnummer og korrelasjons-ID. Generisk HTTP-sporing er deaktivert for DHG-verten. En innkommende korrelasjons-ID godtas bare når den er en GUID.

## Nettverk og svar

Konfigurerte eksterne URL-er må bruke HTTPS. Test- og produksjonsendepunkter kan ikke blandes. Gatewayen godtar bare den konfigurerte offentlige `Host`-verdien og et loopback-basert oppstrøms-API.

FHIR-svar bruker `Cache-Control: no-store`. Swagger og OpenAPI er deaktivert som standard når applikasjons- eller DHG-miljøet er produksjon. Når `Swagger:EnabledInProduction=true`, krever rutene HelseID-tilgangsomfanget `population.read`.
