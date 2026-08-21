targetScope = 'resourceGroup'

@description('Prefix used for the Container App name.')
@minLength(3)
@maxLength(20)
param namePrefix string

@description('Azure region used by the existing Container Apps environment.')
param location string = resourceGroup().location

@description('Name of the existing Azure Container Registry.')
param registryName string

@description('Name of the existing Azure Container Apps managed environment.')
param environmentName string

@description('Name of the existing user-assigned identity with AcrPull on the registry.')
param pullIdentityName string

@description('Immutable container image tag, normally the Git commit SHA.')
param imageTag string

@description('Unique revision suffix used to restart the app after secret rotation or a workflow rerun.')
param revisionSuffix string

@description('DHG Test API base URL. Confirm that the selected Azure network can reach it.')
param dhgBaseUrl string

@description('HelseID Test authority URL.')
param helseIdAuthority string

@description('HelseID client identifier registered for this facade.')
param helseIdClientId string

@description('Audience accepted by the facade when incoming HelseID is enabled outside this test-only deployment.')
param facadeAudience string = 'nhn:population-data-facade'

@description('Scope required by the facade when incoming HelseID is enabled outside this test-only deployment.')
param facadeScope string = 'nhn:population-data-facade/read'

@description('Logical FHIR Patient id for the approved synthetic DHG Test patient.')
param patientTestLogicalId string

@description('Single trusted CIDR allowed through external ingress. Required because this Staging deployment permits Swagger/FHIR without incoming HelseID.')
@minLength(1)
param allowedIpCidr string

@secure()
@description('Auth key for the HelseID TEST token utility. TEST deployment only.')
param helseIdTestTokenAuthKey string

@description('Nine-digit synthetic organization number included in HelseID TEST client claims.')
@minLength(9)
@maxLength(9)
param helseIdTestTokenOrgnrParent string

@description('HelseID TEST client tenancy type approved for this test client.')
@allowed([
  0
  1
  2
])
param helseIdTestTokenClientTenancyType int

@description('Client name included in the synthetic HelseID TEST client claims.')
@minLength(1)
param helseIdTestTokenClientName string

@secure()
@description('National identity number for an approved synthetic DHG Test patient.')
param patientTestNin string

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: registryName
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: environmentName
}

resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: pullIdentityName
}

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-api'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${pullIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: 8080
        transport: 'auto'
        ipSecurityRestrictions: [
          {
            name: 'configured-test-range'
            description: 'CIDR configured by the azure-test GitHub environment.'
            ipAddressRange: allowedIpCidr
            action: 'Allow'
          }
        ]
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: pullIdentity.id
        }
      ]
      secrets: [
        {
          name: 'helseid-test-token-auth-key'
          value: helseIdTestTokenAuthKey
        }
        {
          name: 'patient-test-nin'
          value: patientTestNin
        }
      ]
    }
    template: {
      revisionSuffix: revisionSuffix
      containers: [
        {
          name: 'api'
          image: '${registry.properties.loginServer}/fhir-gravid-api:${imageTag}'
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Staging'
            }
            {
              name: 'ASPNETCORE_HTTP_PORTS'
              value: '8081'
            }
            {
              name: 'Dhg__Environment'
              value: 'Test'
            }
            {
              name: 'Dhg__BaseUrl'
              value: dhgBaseUrl
            }
            {
              name: 'HelseId__Authority'
              value: helseIdAuthority
            }
            {
              name: 'HelseId__FacadeAudience'
              value: facadeAudience
            }
            {
              name: 'HelseId__FacadeScope'
              value: facadeScope
            }
            {
              name: 'HelseId__DhgAudience'
              value: 'nhn:maternity-record'
            }
            {
              name: 'HelseId__DhgScope'
              value: 'nhn:maternity-record/api'
            }
            {
              name: 'HelseId__ClientId'
              value: helseIdClientId
            }
            {
              name: 'HelseIdTestToken__Enabled'
              value: 'true'
            }
            {
              name: 'HelseIdTestToken__AuthKey'
              secretRef: 'helseid-test-token-auth-key'
            }
            {
              name: 'HelseIdTestToken__Audience'
              value: 'nhn:maternity-record'
            }
            {
              name: 'HelseIdTestToken__Scope'
              value: 'nhn:maternity-record/api'
            }
            {
              name: 'HelseIdTestToken__OrgnrParent'
              value: helseIdTestTokenOrgnrParent
            }
            {
              name: 'HelseIdTestToken__ClientTenancy'
              value: 'true'
            }
            {
              name: 'HelseIdTestToken__ClientTenancyType'
              value: string(helseIdTestTokenClientTenancyType)
            }
            {
              name: 'HelseIdTestToken__ClientName'
              value: helseIdTestTokenClientName
            }
            {
              name: 'PatientContext__TestAliases__synthetic_1__LogicalId'
              value: patientTestLogicalId
            }
            {
              name: 'PatientContext__TestAliases__synthetic_1__NationalIdentityNumber'
              secretRef: 'patient-test-nin'
            }
            {
              name: 'DevelopmentTestMode__Enabled'
              value: 'true'
            }
            {
              name: 'DevelopmentTestMode__AllowRemoteStaging'
              value: 'true'
            }
            {
              name: 'Swagger__EnabledInProduction'
              value: 'false'
            }
            {
              name: 'ReverseProxy__ForwardedHeadersEnabled'
              value: 'true'
            }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health/live'
                port: 8081
                scheme: 'HTTP'
              }
              initialDelaySeconds: 1
              periodSeconds: 5
              timeoutSeconds: 2
              failureThreshold: 30
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8081
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 15
              timeoutSeconds: 2
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8081
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 10
              timeoutSeconds: 2
              failureThreshold: 3
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
        {
          name: 'auth-gateway'
          image: '${registry.properties.loginServer}/fhir-gravid-auth-gateway:${imageTag}'
          env: [
            {
              name: 'AUTH_GATEWAY_MODE'
              value: 'passthrough'
            }
            {
              name: 'AUTH_GATEWAY_LISTEN_ADDR'
              value: ':8080'
            }
            {
              name: 'AUTH_GATEWAY_UPSTREAM_URL'
              value: 'http://127.0.0.1:8081'
            }
            {
              name: 'AUTH_GATEWAY_EXTERNAL_SCHEME'
              value: 'https'
            }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 1
              periodSeconds: 5
              timeoutSeconds: 2
              failureThreshold: 30
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 15
              timeoutSeconds: 2
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 10
              timeoutSeconds: 2
              failureThreshold: 3
            }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

output applicationName string = app.name
output applicationUrl string = 'https://${app.properties.configuration.ingress.fqdn}'
