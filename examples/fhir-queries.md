# FHIR query examples

These examples use placeholders only. Never place a NIN, access token, DPoP proof, private key, or patient-context value in source control or shell history.

```powershell
$facadeBase = "https://localhost:7184"
$logicalPatientId = "patient-test-1"
$accessToken = "<short-lived-access-token>"
$dpopProof = "<request-specific-dpop-proof>"
$patientContext = "<short-lived-protected-context>"
```

## Capability statement

`GET /fhir/metadata` is anonymous:

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

All populated observations:

```powershell
Invoke-RestMethod -Uri "$facadeBase/fhir/Observation?patient=$logicalPatientId" -Headers $headers
```

A stable facade fact (`system|code` is URI-encoded by `EscapeDataString`):

```powershell
$token = [Uri]::EscapeDataString("urn:nhn:population-data|pre-pregnancy-bmi")
Invoke-RestMethod -Uri "$facadeBase/fhir/Observation?patient=$logicalPatientId&code=$token" -Headers $headers
```

Latest recorded gestational age:

```powershell
$token = [Uri]::EscapeDataString("urn:nhn:population-data|recorded-gestational-age")
Invoke-RestMethod -Uri "$facadeBase/fhir/Observation?patient=$logicalPatientId&code=$token" -Headers $headers
```

## Encounter search

```powershell
Invoke-RestMethod -Uri "$facadeBase/fhir/Encounter?patient=$logicalPatientId" -Headers $headers
```

An empty supported search returns a `searchset` Bundle with `total=0`. Authentication, patient-context, source, or contract failures return a FHIR `OperationOutcome` with an appropriate HTTP status.
