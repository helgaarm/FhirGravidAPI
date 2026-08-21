# Patient ID and protected context for testing

This facade deliberately separates the public FHIR patient ID from the national identity number (NIN). The logical `patientId` is chosen in configuration; it is not generated from DHG data and is never the NIN.

## Values used in the flow

| Value | Example | Purpose |
|---|---|---|
| Alias | `synthetic_1` | Non-clinical lookup name used only by the Test-support endpoint |
| Logical patient ID | `patient-test-1` | FHIR `Patient.id`, route `{id}`, and `patient` search value |
| NIN | approved synthetic value | Secret identifier sent only in DHG's required outbound header |
| Patient context | protected opaque string | Short-lived binding between logical ID, NIN, subject, and expiry |

The alias and logical ID are not DHG identifiers. An operator selects stable, non-sensitive names for them. Only the configured NIN identifies the synthetic person to DHG.

## 1. Configure a synthetic test patient

For local Development, keep the NIN outside the repository with .NET user-secrets:

```powershell
$project = "src/PopulationDataFacade.Api"

dotnet user-secrets set "PatientContext:TestAliases:synthetic_1:LogicalId" "patient-test-1" --project $project
dotnet user-secrets set "PatientContext:TestAliases:synthetic_1:NationalIdentityNumber" "<approved-synthetic-test-nin>" --project $project
```

Restart the API after changing the configuration. Use only an approved synthetic DHG Test person.

For the Azure Test deployment, `PATIENT_TEST_LOGICAL_ID` is a GitHub Environment variable and `PATIENT_TEST_NIN` is a GitHub Environment secret. The Bicep template creates the same `synthetic_1` mapping in the Container App.

## 2. Issue the protected context

In Swagger, execute:

```text
POST /test/patient-context/synthetic_1
```

The endpoint looks up the configured alias and returns:

```json
{
  "patientId": "patient-test-1",
  "patientContext": "<short-lived-protected-value>"
}
```

The response does not contain the NIN. `patientId` is exactly the configured `LogicalId`. The protected value contains the logical ID, NIN, subject binding, and expiry; its default lifetime is ten minutes.

## 3. Call the FHIR endpoints

For a Patient read in Swagger, enter:

```text
GET /fhir/Patient/{id}

id: patient-test-1
X-Patient-Context: <patientContext returned by the POST>
```

Use the same pair for searches:

```text
GET /fhir/Observation?patient=patient-test-1
GET /fhir/Encounter?patient=patient-test-1
```

The logical ID in the route or search parameter must exactly match the logical ID inside the protected context. Never put the NIN in `{id}` or a query parameter.

PowerShell example for explicit local Development-test mode:

```powershell
$facadeBase = "https://localhost:7184"
$selection = Invoke-RestMethod `
  -Method Post `
  -Uri "$facadeBase/test/patient-context/synthetic_1"

$headers = @{
  "X-Patient-Context" = $selection.patientContext
  Accept = "application/fhir+json"
}

Invoke-RestMethod `
  -Uri "$facadeBase/fhir/Patient/$($selection.patientId)" `
  -Headers $headers
```

## Optional local search by synthetic NIN

An explicit local `DevelopmentTestMode` host also exposes FHIR POST `_search` operations that do not require `X-Patient-Context`. They accept only an 11-digit NIN that exactly matches one configured test alias. The NIN is supplied as an `application/x-www-form-urlencoded` request body and is never returned; resources continue to use the alias's configured logical `patientId`.

In Swagger, use:

```text
POST /fhir/Patient/_search
  identifier: <approved-configured-synthetic-nin>

POST /fhir/Observation/_search
  patient.identifier: <approved-configured-synthetic-nin>
  code: <optional-system|code>

POST /fhir/Encounter/_search
  patient.identifier: <approved-configured-synthetic-nin>
```

PowerShell example:

```powershell
$facadeBase = "https://localhost:7184"
$approvedSyntheticNin = Read-Host "Approved configured synthetic NIN"

Invoke-RestMethod `
  -Method Post `
  -Uri "$facadeBase/fhir/Observation/_search" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{ "patient.identifier" = $approvedSyntheticNin }
```

Do not use `GET /fhir/Observation?patient.identifier=<NIN>` or an equivalent Patient/Encounter URL. Query strings can be retained in browser history, ingress logs, access telemetry, and intermediary systems. The POST routes are intentionally absent from remote Staging, QA, and Production. An unknown or unconfigured NIN returns `404` without echoing the supplied value.

## Common responses

| Response | Meaning |
|---:|---|
| `404` from the Test-support POST | The alias is not configured; configure both `LogicalId` and `NationalIdentityNumber`, then restart |
| `400` from a FHIR operation | The context header is missing, malformed, or expired |
| `404` saying the patient was not found in this context | The route/search logical ID does not match the returned `patientId` |
| `403` from a DHG-backed operation | DHG reports missing consent or another forbidden state |
| `404` from a DHG-backed operation | DHG reports no active maternity record or no matching synthetic patient |
| `500`/`503` mentioning HelseID or DHG | Check TEST credentials, claims, endpoint connectivity, and the API terminal log/correlation ID |

Issue a new context after its lifetime expires or after an application restart that replaces local Data Protection keys. Treat the context as sensitive and do not paste it into source control, logs, issues, or chat.

## Environment boundary

The alias endpoint is test support, not a production patient-selection protocol. It is unavailable when the host or DHG security boundary is Production. A production patient-context authority and trust contract must be approved and implemented before clinical deployment.
