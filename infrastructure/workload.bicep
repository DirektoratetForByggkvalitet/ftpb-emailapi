param location string
param appName string
param rgSharedResources string
param aspName string
param privateDnsZoneName string
param vnetName string
param subnetName string
param connectivitySubnet string
param startCommand string = ''

// Container image settings — the app runs as a container pulled from an existing shared ACR.
param acrName string
param acrResourceGroup string
param imageName string
param imageTag string

resource appServicePlan 'Microsoft.Web/serverfarms@2021-01-15' existing = {
  name: aspName
  scope: resourceGroup(rgSharedResources)
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: acrName
  scope: resourceGroup(acrResourceGroup)
}

var linuxFxVersion = 'DOCKER|${containerRegistry.properties.loginServer}/${imageName}:${imageTag}'

var siteConfig = {
  linuxFxVersion: linuxFxVersion
  // Pull the image using the site's system-assigned managed identity (no registry admin creds).
  acrUseManagedIdentityCreds: true
  healthCheckPath: '/health'
  appCommandLine: startCommand
  appSettings: [
    {
      // Container listens on 8080 (non-root); tell App Service which port to forward to.
      name: 'WEBSITES_PORT'
      value: '8080'
    }
    {
      name: 'WEBSITE_WEBDEPLOY_USE_SCM'
      value: 'false'
    }
    {
      name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
      value: 'false'
    }
  ]
}

resource AppServiceApp 'Microsoft.Web/sites@2022-09-01' = {
  name: appName
  location: location
  identity: {
    type: 'SystemAssigned'
  }

  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    virtualNetworkSubnetId: resourceId(rgSharedResources, 'Microsoft.Network/virtualNetworks/subnets', vnetName, connectivitySubnet)
    siteConfig: siteConfig
  }
}

resource stagingSlot 'Microsoft.Web/sites/slots@2022-09-01' = {
  name: 'staging'
  parent: AppServiceApp
  location: location
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: siteConfig
  }
}

// Both the production site and the staging slot have their own managed identity,
// so both need AcrPull to pull the image from the shared registry.
module assignAcrPullApp 'AssignAcrPull.bicep' = {
  name: 'assignAcrPull-app'
  scope: resourceGroup(acrResourceGroup)
  params: {
    acrName: acrName
    principalId: AppServiceApp.identity.principalId
  }
}

module assignAcrPullSlot 'AssignAcrPull.bicep' = {
  name: 'assignAcrPull-slot'
  scope: resourceGroup(acrResourceGroup)
  params: {
    acrName: acrName
    principalId: stagingSlot.identity.principalId
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
