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

param appConfigurationUri string

param azureTenantId string

param managedIdentityName string = 'uid-${appName}'

resource appServicePlan 'Microsoft.Web/serverfarms@2021-01-15' existing = {
  name: aspName
  scope: resourceGroup(rgSharedResources)
}

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

var siteConfig = {
  acrUseManagedIdentityCreds: true
  acrUserManagedIdentityID: managedIdentity.properties.clientId
  healthCheckPath: '/health'
  appSettings: [
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
      name: 'GraphApiAuth__ManagedIdentityClientId'
      value: managedIdentity.properties.clientId
    }
  ]
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
