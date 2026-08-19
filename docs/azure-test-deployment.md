# Azure test deployment from GitHub

This repository contains a test-only deployment path for Azure Container Apps. Pushes and pull requests build, test, compile both Bicep templates, and build the container. Deployment is deliberately manual (`workflow_dispatch`) and cannot start until the protected GitHub environment contains the required HelseID Test values.

The solution does not use Azure Key Vault. Private JWKs and the synthetic test NIN are stored as GitHub Environment secrets, passed to Bicep as secure parameters, and installed as Container App secrets. GitHub authenticates to Azure through OIDC, so no Azure client secret is stored in GitHub.

## Security boundary

The Azure template always runs with these constraints:

- host environment `Staging` and `Dhg:Environment=Test`;
- anonymous incoming Swagger/FHIR test mode, while outgoing DHG calls still use a DPoP-bound HelseID client-credentials token;
- one mandatory trusted ingress CIDR (`AZURE_ALLOWED_IP_CIDR`);
- HTTPS-only external ingress;
- one replica, with private Data Protection keys kept only in that replica;
- immutable images tagged with the Git commit SHA;
- ACR admin credentials disabled and image pull through a user-assigned managed identity.

Do not reuse this workflow or `AllowRemoteStaging` for production. When either the ASP.NET host or DHG is Production, the application rejects test mode and requires incoming HelseID/DPoP for clinical FHIR operations. Production also disables Swagger by default.

The Container Apps ingress restriction is part of the remote test-mode security boundary. Use the public CIDR of the controlled NHN test network or workstation egress, never `0.0.0.0/0`. The workflow intentionally cannot deploy without this value.

## 1. Decide the network before creating the environment

Confirm that the selected Azure region/network can reach both the DHG Test base URL and HelseID Test. The published DHG endpoint may require Helsenett connectivity. If it is not reachable over the public internet, provide a delegated Container Apps infrastructure subnet with the approved VPN, route, firewall, or proxy path when deploying `foundation.bicep`.

The Container Apps environment network type cannot be changed in place. Do not create the foundation with an empty `infrastructureSubnetId` until connectivity has been confirmed.

## 2. Create the one-time Azure foundation

Run this once as an Azure identity that may create role assignments. Replace the example values and supply the subnet resource ID when required:

```powershell
$subscriptionId = "<subscription-id>"
$resourceGroup = "rg-fhir-gravid-test"
$location = "norwayeast"
$namePrefix = "fhir-gravid-test"
$infrastructureSubnetId = "" # required here if DHG Test needs private routing

az account set --subscription $subscriptionId
az group create --name $resourceGroup --location $location
az deployment group create `
  --name fhir-gravid-foundation `
  --resource-group $resourceGroup `
  --template-file infra/foundation.bicep `
  --parameters `
    namePrefix=$namePrefix `
    location=$location `
    infrastructureSubnetId=$infrastructureSubnetId
```

Record the deployment outputs. They become GitHub Environment variables:

```powershell
az deployment group show `
  --name fhir-gravid-foundation `
  --resource-group $resourceGroup `
  --query properties.outputs
```

## 3. Configure GitHub OIDC

Create or reuse a Microsoft Entra application/service principal for GitHub deployment. Add the federated credential in [the example file](../infra/github-federated-credential.example.json):

```powershell
$deploymentAppClientId = "<entra-application-client-id>"
az ad app federated-credential create `
  --id $deploymentAppClientId `
  --parameters infra/github-federated-credential.example.json
```

The example uses the normal protected-environment subject:

```text
repo:helgaarm/FhirGravidAPI:environment:azure-test
```

If the GitHub organization has customized OIDC subject claims (for example an immutable `repository_id` claim), replace the example subject with the actual subject configured for this repository before creating the Entra credential.

Grant the service principal only the deployment roles it needs after the foundation exists:

```powershell
$deploymentSpObjectId = az ad sp show --id $deploymentAppClientId --query id --output tsv
$resourceGroupId = az group show --name $resourceGroup --query id --output tsv
$registryId = az acr show --name "<registryName-output>" --resource-group $resourceGroup --query id --output tsv

az role assignment create `
  --assignee-object-id $deploymentSpObjectId `
  --assignee-principal-type ServicePrincipal `
  --role Contributor `
  --scope $resourceGroupId
az role assignment create `
  --assignee-object-id $deploymentSpObjectId `
  --assignee-principal-type ServicePrincipal `
  --role AcrPush `
  --scope $registryId
```

The GitHub deployment identity does not need Owner or User Access Administrator because the workflow does not create the foundation or role assignments.

## 4. Create the protected GitHub environment

In GitHub, open **Settings → Environments**, create `azure-test`, and configure required reviewers. Limit deployment branches to `main`.

Add these environment variables:

| Variable | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | Entra application client ID used by GitHub OIDC |
| `AZURE_TENANT_ID` | Azure tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |
| `AZURE_RESOURCE_GROUP` | Dedicated test resource group |
| `AZURE_LOCATION` | `location` foundation output |
| `AZURE_NAME_PREFIX` | Same prefix used for the foundation |
| `AZURE_CONTAINER_REGISTRY` | `registryName` foundation output |
| `AZURE_CONTAINERAPPS_ENVIRONMENT` | `environmentName` foundation output |
| `AZURE_PULL_IDENTITY` | `pullIdentityName` foundation output |
| `AZURE_ALLOWED_IP_CIDR` | Trusted public egress CIDR, never `0.0.0.0/0` |
| `DHG_BASE_URL` | Confirmed reachable DHG Test API base URL |
| `HELSEID_AUTHORITY` | HelseID Test authority |
| `HELSEID_CLIENT_ID` | HelseID client authorized for DHG Test |
| `PATIENT_TEST_LOGICAL_ID` | Logical FHIR id for the approved synthetic patient |

Add these environment secrets before the first deployment:

| Secret | Value |
| --- | --- |
| `HELSEID_CLIENT_ASSERTION_JWK_B64` | Base64-encoded private client-assertion JWK JSON |
| `HELSEID_DPOP_JWK_B64` | Base64-encoded, separate private DPoP JWK JSON |
| `PATIENT_TEST_NIN` | NIN of the approved synthetic DHG Test patient |

Base64 is used only to transport structured JSON reliably through GitHub Actions; it is not encryption. GitHub Environment protection is the security control. Encode each JWK in PowerShell without printing it:

```powershell
$clientAssertionB64 = [Convert]::ToBase64String(
  [Text.Encoding]::UTF8.GetBytes((Get-Content .\client-assertion.private.jwk -Raw)))
$clientAssertionB64 | gh secret set HELSEID_CLIENT_ASSERTION_JWK_B64 --env azure-test
Remove-Variable clientAssertionB64

$dpopB64 = [Convert]::ToBase64String(
  [Text.Encoding]::UTF8.GetBytes((Get-Content .\dpop.private.jwk -Raw)))
$dpopB64 | gh secret set HELSEID_DPOP_JWK_B64 --env azure-test
Remove-Variable dpopB64

gh secret set PATIENT_TEST_NIN --env azure-test
```

Never put private JWK JSON, NIN, tokens, or Azure client secrets in repository files, workflow variables, command history, or issue text.

## 5. Deploy and test

Open **Actions → Verify and deploy Azure test → Run workflow**. The job first repeats the complete verification, authenticates with OIDC, pushes an SHA-tagged image, and deploys the Container App. It stops with a list of missing configuration names when HelseID has not yet been configured.

Because ingress is restricted, the GitHub-hosted runner cannot perform external smoke tests. Container Apps performs startup, liveness, and readiness probes internally. From the trusted CIDR, verify:

```powershell
$baseUrl = "https://<container-app-fqdn>"
curl.exe -f "$baseUrl/health/live"
curl.exe -f "$baseUrl/health/ready"
Start-Process "$baseUrl/swagger"
```

In Swagger, issue a context for `synthetic_1`, copy `patientId` and `patientContext`, then use the context header on a FHIR call. Incoming Swagger/FHIR calls do not need HelseID in this test-only deployment; the facade obtains HelseID credentials server-side for DHG.

Readiness currently confirms only that the process is running. A successful readiness response is not proof that HelseID or DHG is reachable. The first authorized DHG-backed FHIR operation is the external integration test.

## Operational limitations

- Data Protection keys are not persisted because this test deployment uses neither Key Vault nor external storage. A restart or new revision invalidates already issued ten-minute patient contexts; issue a new context.
- Container App secrets are application-scoped and changing a secret alone does not restart a revision. The workflow forces a new single-active revision on every manual run, so rotate the GitHub secret and redeploy when a private key or NIN is replaced.
- The deployment uses one replica. Do not increase the replica count without a shared Data Protection key store and a new architecture decision.
- Do not treat this setup as a production pattern. Production needs approved network design, secret lifecycle, persistent Data Protection, HelseID ingress integration, monitoring, and privacy/security review.

## Primary references

- [GitHub Actions OIDC with Azure](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-azure)
- [GitHub deployment environments](https://docs.github.com/en/actions/concepts/workflows-and-actions/deployment-environments)
- [GitHub Actions secrets](https://docs.github.com/en/actions/reference/security/secrets)
- [Azure Container Apps secrets](https://learn.microsoft.com/azure/container-apps/manage-secrets)
- [Azure Container Apps ingress](https://learn.microsoft.com/azure/container-apps/ingress-overview)
- [Azure Container Apps health probes](https://learn.microsoft.com/azure/container-apps/health-probes)
- [Managed identity image pull from ACR](https://learn.microsoft.com/azure/container-apps/managed-identity-image-pull)
