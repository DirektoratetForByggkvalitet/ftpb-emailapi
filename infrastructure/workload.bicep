param location string
param appName string
param rgSharedResources string
param aspName string
param privateDnsZoneName string
param vnetName string
param subnetName string
param connectivitySubnet string

param acrName string
param acrResourceGroup string

param aspNetCoreEnvironment string

param appConfigurationUri string = ''

param azureTenantId string = ''

param managedIdentityName string = 'uid-${appName}'

// Graph authentication. Today the app uses a client secret, as it always has. 
// TODO: When the federated identity credential is in place on the app registration, set this to true: the app then
// authenticates as that app registration via the managed identity, and the secret is no longer read.
// Nothing else changes, and graphApiClientSecret can be left empty from then on.
param useFederatedIdentityCredential bool = false

@secure()
param graphApiClientSecret string = ''

param graphApiTenantId string
param graphApiClientId string

param emailUserPrincipalName string
param emailDefaultFromAddress string
param emailDefaultFromDisplayName string

param serilogConnectionUrl string
param serilogUsername string
@secure()
param serilogPassword string
param serilogIndexFormat string = 'logs-emailservice'

param elasticApmServerUrl string
@secure()
param elasticApmSecretToken string
param elasticApmEnvironment string = aspNetCoreEnvironment

param loggingLogLevelDefault string = 'Debug'

resource appServicePlan 'Microsoft.Web/serverfarms@2021-01-15' existing = {
  name: aspName
  scope: resourceGroup(rgSharedResources)
}

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

var graphAuthSettings = useFederatedIdentityCredential
  ? [
      {
        name: 'GraphApiAuth__ManagedIdentityClientId'
        value: managedIdentity.properties.clientId
      }
    ]
  : [
      {
        name: 'GraphApiAuth__ClientSecret'
        value: graphApiClientSecret
      }
    ]

var siteConfig = {
  acrUseManagedIdentityCreds: true
  acrUserManagedIdentityID: managedIdentity.properties.clientId
  healthCheckPath: '/health'
  appSettings: concat([
    {
      name: 'WEBSITES_PORT'
      value: '8080'
    }
    {
      name: 'ASPNETCORE_ENVIRONMENT'
      value: aspNetCoreEnvironment
    }
    {
      name: 'AppConfiguration_Uri'
      value: appConfigurationUri
    }
    {
      name: 'Azure__TenantId'
      value: azureTenantId
    }
    {
      name: 'AZURE_CLIENT_ID'
      value: managedIdentity.properties.clientId
    }
    {
      name: 'GraphApiAuth__TenantId'
      value: graphApiTenantId
    }
    {
      name: 'GraphApiAuth__ClientId'
      value: graphApiClientId
    }
    {
      name: 'GraphApiEmailSettings__UserPrincipalName'
      value: emailUserPrincipalName
    }
    {
      name: 'GraphApiEmailSettings__DefaultFromAddress'
      value: emailDefaultFromAddress
    }
    {
      name: 'GraphApiEmailSettings__DefaultFromDisplayName'
      value: emailDefaultFromDisplayName
    }
    {
      name: 'Serilog__ConnectionUrl'
      value: serilogConnectionUrl
    }
    {
      name: 'Serilog__Username'
      value: serilogUsername
    }
    {
      name: 'Serilog__Password'
      value: serilogPassword
    }
    {
      name: 'Serilog__IndexFormat'
      value: serilogIndexFormat
    }
    {
      name: 'ElasticApm__ServerUrl'
      value: elasticApmServerUrl
    }
    {
      name: 'ElasticApm__SecretToken'
      value: elasticApmSecretToken
    }
    {
      name: 'ElasticApm__Environment'
      value: elasticApmEnvironment
    }
    {
      name: 'Logging__LogLevel__Default'
      value: loggingLogLevelDefault
    }
  ], graphAuthSettings)
}

resource AppServiceApp 'Microsoft.Web/sites@2022-09-01' = {
  name: appName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }

  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    virtualNetworkSubnetId: resourceId(rgSharedResources, 'Microsoft.Network/virtualNetworks/subnets', vnetName, connectivitySubnet)
    siteConfig: siteConfig
  }
}

module assignAcrPullApp 'AssignAcrPull.bicep' = {
  name: 'assignAcrPull-app'
  scope: resourceGroup(acrResourceGroup)
  params: {
    acrName: acrName
    principalId: managedIdentity.properties.principalId
  }
}

var privateEndpointName = 'pe-${appName}'

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = {
  name: privateEndpointName
  location: location
  properties: {
    subnet: {
      id: resourceId(rgSharedResources, 'Microsoft.Network/virtualNetworks/subnets', vnetName, subnetName)
    }
    privateLinkServiceConnections: [
      {
        name: privateEndpointName
        properties: {
          groupIds: ['sites']
          privateLinkServiceId: AppServiceApp.id
        }
      }
    ]
  }
}

module addToPrivateDns 'AddToPrivateDns.bicep' = {
  name: 'addToPrivateDns'
  params: {
    privateDnsZoneName: privateDnsZoneName
    privateEndpointName: privateEndpointName
    appResourceGroupName: resourceGroup().name
    appName: appName
  }
  dependsOn: [privateEndpoint]
  scope: resourceGroup(rgSharedResources)
}

output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output managedIdentityClientId string = managedIdentity.properties.clientId
