targetScope = 'resourceGroup'

@description('Short lowercase prefix used for all Azure resource names.')
@minLength(3)
@maxLength(20)
param namePrefix string

@description('Azure region for the test environment.')
param location string = resourceGroup().location

@description('Optional delegated Container Apps infrastructure subnet. Decide this before creating the environment; leave empty only when public Azure connectivity is sufficient for DHG Test.')
param infrastructureSubnetId string = ''

var normalizedPrefix = toLower(replace(namePrefix, '-', ''))
var registryName = take('${normalizedPrefix}${uniqueString(resourceGroup().id)}', 50)
var acrPullRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d'
)

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-logs'
  location: location
  properties: {
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
    vnetConfiguration: empty(infrastructureSubnetId) ? null : {
      infrastructureSubnetId: infrastructureSubnetId
      internal: false
    }
  }
}

resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-acr-pull'
  location: location
}

resource registryPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, pullIdentity.id, acrPullRoleDefinitionId)
  scope: registry
  properties: {
    roleDefinitionId: acrPullRoleDefinitionId
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output registryName string = registry.name
output environmentName string = environment.name
output pullIdentityName string = pullIdentity.name
output pullIdentityResourceId string = pullIdentity.id
output location string = location
