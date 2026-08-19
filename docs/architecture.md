# Arkitektur

## Flyt

```text
FHIR-klient
  │ HelseID DPoP access-token + beskyttet pasientkontekst
  ▼
PopulationDataFacade.Api
  │ validerer token/scope/kontekst, kjenner ikke DHG JSON
  ▼
IPopulationDataService / Core-modell
  │
  ▼
Infrastructure
  ├─ HelseID token exchange + DPoP
  ├─ GET DHG /status
  ├─ GET DHG /record/{latestRecordId}
  └─ DHG DTO → kildeuavhengig PopulationSnapshot
  │
  ▼
Firely FHIR R4-mapping → Patient / Observation / Encounter / Bundle
```

I eksplisitt `DevelopmentTestMode` er Swagger/FHIR-siden anonym. Den innkommende token-exchange-flyten erstattes da av en server-side HelseID `client_credentials`-forespørsel med DHG resource/scope og DPoP. Modusen er normalt sperret til lokal `Development` sammen med `Dhg:Environment=Test`. Repositoryets testmal kan eksplisitt tillate `Staging` med `AllowRemoteStaging=true`, men bare bak malens obligatoriske Container Apps CIDR-begrensning. Resten av DHG-status/record- og FHIR-flyten er uendret, og begge testvariantene avvises mot Production.

API-laget arbeider bare med `PopulationSnapshot`. DHG JSON-stier, headernavn og wire-kontrakter finnes i Infrastructure. Det gjør at FHIR-kontrakten kan testes uten runtime-mock eller alternativ datakilde.

## Prosjekter og ansvar

| Prosjekt | Ansvar |
|---|---|
| Core | Kildeuavhengige kliniske verdityper, koder og Firely FHIR-mapping |
| Infrastructure | DHG-kontrakt, status/record-orkestrering, HelseID, DPoP, HTTP-resiliens og kilde-mapping |
| Api | Autentisering, autorisasjon, beskyttet pasientkontekst, FHIR-endepunkter, feilformat og observability |
| Tests | Wire-kontrakt, semantiske mappingregler og in-process HTTP-kontrakt |

## Konsistens og levetid

Hver forespørsel leser `/status` og deretter det oppgitte `latestRecordId`. Record-ID og status `ACTIVE` verifiseres før mapping. Det finnes ingen tverrforespørselscache av pasient- eller tokendata i fasaden. Status og record kan i teorien endres mellom de to kallene; mismatch eller inaktiv record blir en kontrollert feil, ikke et delvis datasett.

## Resiliens

Retries gjelder kun DHG GET og kun nettverksfeil, timeout, 408, 429, 502, 503 og 504. Backoff har jitter og respekterer `Retry-After`. Et `DPoP-Nonce`-challenge kan retries én gang med nytt proof. Andre 401/403 retries ikke. Token exchange gjøres per DHG-kall for å unngå en delt token-cache med brukerbundet materiale.

## FHIR-valg

- `Patient` er minimal og inneholder ikke navn, adresse, fødselsnummer eller annen demografi.
- eksplisitte DHG-fakta blir hovedsakelig `Observation` med korrekt FHIR datatype.
- kontrollbesøk blir `Encounter`; målinger kan referere til besøket.
- nullable boolean mappes bare når verdien finnes. `false` beholdes som `valueBoolean: false`.
- source `lastUpdated` går til `meta.lastUpdated`; måledato går til `effective[x]`.
- ukjente kodesystemer beholdes. Numeriske OID-er normaliseres til `urn:oid:`.
