# Eksempler på FHIR-spørringer

Eksemplene bruker plassholdere. Ikke legg fødselsnummer, tilgangstoken, DPoP-bevis, private nøkler eller pasientkontekst i kildekontroll eller skallhistorikk. I autentisert modus brukes HTTPS-adressen foran `auth-gateway`.

```powershell
$facadeBase = "https://facade.example.test"
$logicalPatientId = "patient-test-1"
$accessToken = "<short-lived-access-token>"
$dpopProof = "<request-specific-dpop-proof>"
$patientContext = "<short-lived-protected-context>"
```

Hvert DPoP-bevis gjelder én forespørsel. `htu` og `htm` må samsvare med den endelige URL-en og HTTP-metoden. Vertsnavnet må samsvare med `AUTH_GATEWAY_EXTERNAL_HOST`.

I lokal `DevelopmentTestMode` settes `$facadeBase` til API-adressen, normalt `https://localhost:7184`, og headerne `Authorization` og `DPoP` utelates.

## Velg en lokal testpasient

Den logiske pasient-ID-en konfigureres lokalt og er ikke et fødselsnummer eller en verdi fra DHG. Konfigurer aliaset `synthetic_1` som beskrevet i [pasient-ID og beskyttet pasientkontekst](../docs/patient-context-testing.md), og hent verdiene:

```powershell
$selection = Invoke-RestMethod `
  -Method Post `
  -Uri "$facadeBase/test/patient-context/synthetic_1"

$logicalPatientId = $selection.patientId
$patientContext = $selection.patientContext
```

Pasientkonteksten er kortlivet. Ikke erstatt `$logicalPatientId` med et fødselsnummer.

## POST-søk med fødselsnummer

Les fødselsnummeret interaktivt og send det i skjemaet, ikke i URL-en. Disse fire POST-søkene bruker ikke `X-Patient-Context`:

```powershell
$patientNin = Read-Host "Fødselsnummer"

Invoke-RestMethod `
  -Method Post `
  -Uri "$facadeBase/fhir/Patient/_search" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{ identifier = $patientNin }

Invoke-RestMethod `
  -Method Post `
  -Uri "$facadeBase/fhir/Observation/_search" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{
    "patient.identifier" = $patientNin
    category = "vital-signs"
    date = "ge2026-01-01"
  }

Invoke-RestMethod `
  -Method Post `
  -Uri "$facadeBase/fhir/Encounter/_search" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{ "patient.identifier" = $patientNin }

Invoke-RestMethod `
  -Method Post `
  -Uri "$facadeBase/fhir/CareTeam/_search" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{ "patient.identifier" = $patientNin }
```

I lokal `DevelopmentTestMode` utelates autentiseringsheaderne. Fødselsnummeret må samsvare med ett konfigurert alias, og returnerte ressurser bruker aliasets logiske ID.

I autentisert drift må hvert kall ha HelseID-headerne nedenfor. `$dpopProof` opprettes for den aktuelle POST-URL-en og kan ikke gjenbrukes:

```powershell
$authenticatedSearchHeaders = @{
  Authorization = "DPoP $accessToken"
  DPoP = $dpopProof
  Accept = "application/fhir+json"
}

Invoke-RestMethod `
  -Method Post `
  -Uri "$facadeBase/fhir/Patient/_search" `
  -Headers $authenticatedSearchHeaders `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{ identifier = $patientNin }
```

Tokenet må oppfylle kravet `population.read`. Svaret bruker en stabil, pseudonym pasient-ID og inneholder ikke fødselsnummeret. Fødselsnummer skal ikke legges i en GET-spørring.

## CapabilityStatement

Direkte kall til API-et er anonymt:

```powershell
Invoke-RestMethod -Uri "$facadeBase/fhir/metadata" -Headers @{ Accept = "application/fhir+json" }
```

Gjennom gatewayen i `authenticate`-modus kreves HelseID-token og DPoP-bevis også for metadata:

```powershell
$metadataHeaders = @{
  Authorization = "DPoP $accessToken"
  DPoP = $dpopProof
  Accept = "application/fhir+json"
}
Invoke-RestMethod -Uri "$facadeBase/fhir/metadata" -Headers $metadataHeaders
```

## Patient-ressurs

```powershell
$headers = @{
  Authorization = "DPoP $accessToken"
  DPoP = $dpopProof
  "X-Patient-Context" = $patientContext
  Accept = "application/fhir+json"
}
Invoke-RestMethod -Uri "$facadeBase/fhir/Patient/$logicalPatientId" -Headers $headers
```

## Søk etter observasjoner

Alle observasjoner:

```powershell
Invoke-RestMethod -Uri "$facadeBase/fhir/Observation?patient=$logicalPatientId" -Headers $headers
```

Datert kroppsvekt (`system|code` URL-kodes med `EscapeDataString`):

```powershell
$token = [Uri]::EscapeDataString("http://loinc.org|29463-7")
Invoke-RestMethod -Uri "$facadeBase/fhir/Observation?patient=$logicalPatientId&code=$token&category=vital-signs&date=ge2026-01-01" -Headers $headers
```

Høyde, vekt og BMI før svangerskapet er FHIR R4-observasjoner med `category=vital-signs`, men uten `effective[x]` fordi DHG ikke leverer måletidspunkt. De inngår derfor ikke i et datofiltrert søk. Eksempel for BMI:

```powershell
$token = [Uri]::EscapeDataString("http://snomed.info/sct|60621009")
Invoke-RestMethod `
  -Uri "$facadeBase/fhir/Observation?patient=$logicalPatientId&code=$token&category=vital-signs" `
  -Headers $headers
```

Historikk for svangerskapsalder bruker LOINC `18185-9`. Klienten velger den nyeste `effectiveDateTime`; fasaden lager ikke en egen «siste»-observasjon:

```powershell
$token = [Uri]::EscapeDataString("http://loinc.org|18185-9")
$gestationalAgeBundle = Invoke-RestMethod `
  -Uri "$facadeBase/fhir/Observation?patient=$logicalPatientId&code=$token" `
  -Headers $headers

$latestGestationalAge = $gestationalAgeBundle.entry.resource |
  Sort-Object effectiveDateTime -Descending |
  Select-Object -First 1
```

Fosterets hjertefrekvens returneres med mor som `subject`. `focus` finnes bare når DHG leverer en positiv `fosterId`. Eksemplet filtrerer derfor bort observasjoner uten `focus`:

```powershell
$token = [Uri]::EscapeDataString("http://snomed.info/sct|364075005")
$fetalBundle = Invoke-RestMethod `
  -Uri "$facadeBase/fhir/Observation?patient=$logicalPatientId&code=$token" `
  -Headers $headers

$fetalObservation = $fetalBundle.entry.resource |
  Where-Object { $_.focus -and $_.focus[0].reference } |
  Select-Object -First 1

if (-not $fetalObservation) { throw "Fant ingen fosterreferanse i svaret." }

$fetusReference = $fetalObservation.focus[0].reference
$fetusPatientId = $fetusReference -replace '^Patient/', ''
Invoke-RestMethod -Uri "$facadeBase/fhir/Patient/$fetusPatientId" -Headers $headers
```

I autentisert modus krever hvert GET-kall et eget DPoP-bevis med korrekt `htu`.

## Søk etter konsultasjoner

```powershell
Invoke-RestMethod -Uri "$facadeBase/fhir/Encounter?patient=$logicalPatientId" -Headers $headers
```

## Søk etter behandlingsteam

Jordmor og helsestasjon returneres bare når de finnes i det aktive DHG-kortet. `Practitioner` og `Organization` ligger som inneholdte ressurser i `CareTeam`; fasaden gjør ingen katalogoppslag:

```powershell
Invoke-RestMethod -Uri "$facadeBase/fhir/CareTeam?patient=$logicalPatientId" -Headers $headers
```

Et søk uten treff returnerer en `searchset`-`Bundle` med `total=0`. Feil ved autentisering, pasientkontekst, DHG eller kontrakt returneres som `OperationOutcome` med passende HTTP-status.
