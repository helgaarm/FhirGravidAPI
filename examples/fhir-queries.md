# Eksempler på FHIR queries

Disse eksemplene bruker bare placeholders. Legg aldri NIN, access token, DPoP proof, private key eller patient-context value i source control eller shell history. I authenticated mode skal du bruke den canonical HTTPS address til trusted TLS ingress foran `auth-gateway`. Ikke kall den private API port eller eksponer gatewayens plaintext port 8080.

```powershell
$facadeBase = "https://facade.example.test"
$logicalPatientId = "patient-test-1"
$accessToken = "<short-lived-access-token>"
$dpopProof = "<request-specific-dpop-proof>"
$patientContext = "<short-lived-protected-context>"
```

Hvert DPoP proof er request-specific: `htu` og `htm` må samsvare med den endelige `$facadeBase` URL-en og HTTP method. Canonical Host må samsvare med `AUTH_GATEWAY_EXTERNAL_HOST`.

Bare for eksplisitt anonymous local Development-test mode setter du `$facadeBase` til API launch URL (normalt `https://localhost:7184`) og utelater `Authorization`- og `DPoP`-headers. Dette direct-API pattern er ikke gyldig for authenticated eller production operation.

## Opprett lokal testpasient-selection

Logical patient ID konfigureres av operatøren og er ikke NIN eller en verdi avledet fra DHG. Konfigurer aliaset `synthetic_1` som beskrevet i [Pasient-ID og protected context for testing](../docs/patient-context-testing.md), og hent deretter begge verdiene som brukes nedenfor:

```powershell
$selection = Invoke-RestMethod `
  -Method Post `
  -Uri "$facadeBase/test/patient-context/synthetic_1"

$logicalPatientId = $selection.patientId
$patientContext = $selection.patientContext
```

Context er short-lived. Erstatt aldri `$logicalPatientId` med NIN.

## POST search med NIN

Les NIN inn interaktivt slik at det ikke skrives på command line, og send det i en form body i stedet for en URL. Disse tre POST searches bruker ikke `X-Patient-Context`:

```powershell
$patientNin = Read-Host "NIN"

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
```

I lokal `DevelopmentTestMode` utelates auth headers, NIN må samsvare med ett konfigurert alias, og returnerte resources bruker aliasets logical ID.

I autentisert drift og Production skal hvert kall i stedet ha HelseID-headerne nedenfor. `$dpopProof` må opprettes spesielt for den aktuelle POST-URL-en og kan ikke gjenbrukes mellom de tre eksemplene:

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

Det autentiserte tokenet må oppfylle fasadens `population.read`-policy. Responsen bruker en stabil HMAC-pseudonym patient ID og inneholder aldri NIN. Legg aldri NIN i en GET query string.

## CapabilityStatement

`GET /fhir/metadata` er anonymous:

```powershell
Invoke-RestMethod -Uri "$facadeBase/fhir/metadata" -Headers @{ Accept = "application/fhir+json" }
```

## Minimal Patient

```powershell
$headers = @{
  Authorization = "DPoP $accessToken"
  DPoP = $dpopProof
  "X-Patient-Context" = $patientContext
  Accept = "application/fhir+json"
}
Invoke-RestMethod -Uri "$facadeBase/fhir/Patient/$logicalPatientId" -Headers $headers
```

## Observation search

Alle populated observations:

```powershell
Invoke-RestMethod -Uri "$facadeBase/fhir/Observation?patient=$logicalPatientId" -Headers $headers
```

Datert body weight (`system|code` URI-encodes av `EscapeDataString`):

```powershell
$token = [Uri]::EscapeDataString("http://loinc.org|29463-7")
Invoke-RestMethod -Uri "$facadeBase/fhir/Observation?patient=$logicalPatientId&code=$token&category=vital-signs&date=ge2026-01-01" -Headers $headers
```

Gestational-age history bruker LOINC `18185-9`. Velg nyeste `effectiveDateTime` client-side; fasaden lager ikke en duplicate facade-specific «latest» Observation:

```powershell
$token = [Uri]::EscapeDataString("http://loinc.org|18185-9")
$gestationalAgeBundle = Invoke-RestMethod `
  -Uri "$facadeBase/fhir/Observation?patient=$logicalPatientId&code=$token" `
  -Headers $headers

$latestGestationalAge = $gestationalAgeBundle.entry.resource |
  Sort-Object effectiveDateTime -Descending |
  Select-Object -First 1
```

## Encounter search

```powershell
Invoke-RestMethod -Uri "$facadeBase/fhir/Encounter?patient=$logicalPatientId" -Headers $headers
```

Et tomt, støttet search returnerer en `searchset` Bundle med `total=0`. Feil knyttet til authentication, patient-context, source eller contract returnerer et FHIR `OperationOutcome` med passende HTTP status.
