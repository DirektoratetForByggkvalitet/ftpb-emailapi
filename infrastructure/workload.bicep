param location string
param appName string
param rgSharedResources string
param aspName string
param privateDnsZoneName string
param vnetName string
param subnetName string
param connectivitySubnet string

// The app runs as a container pulled from the environment's ACR. The image tag is *not* set here:
// the Octopus deployment process points the site at a specific tag on every release, so a runbook
// run must never re-point the site at whatever tag the template happened to be given.
param acrName string
param acrResourceGroup string

// Drives the App Configuration label filter, so it must be set explicitly — an unset value silently
// defaults to Production. The value has to match the labels used in the App Configuration store.
param aspNetCoreEnvironment string

// Endpoint of the shared Azure App Configuration store; the app loads the rest of its settings from
// there at startup. Empty means "no App Configuration", and the app falls back to app settings.
param appConfigurationUri string

// Tenant used by DefaultAzureCredential when reading App Configuration and Key Vault.
param azureTenantId string

resource appServicePlan 'Microsoft.Web/serverfarms@2021-01-15' existing = {
  name: aspName
  scope: resourceGroup(rgSharedResources)
}

var siteConfig = {
  // Pull the image using the site's system-assigned managed identity (no registry admin creds).
  acrUseManagedIdentityCreds: true
  // Liveness only — deliberately not the readiness path, which depends on Entra and Key Vault.
  healthCheckPath: '/health'
  appSettings: [
    {
      // Container listens on 8080 (non-root); tell App Service which port to forward to.
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
      // App Service injects app settings as environment variables, where the section separator is
      // '__'. A colon would only work on Windows.
      name: 'Azure__TenantId'
      value: azureTenantId
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

// The site pulls its own image, so its managed identity needs AcrPull on the registry.
module assignAcrPullApp 'AssignAcrPull.bicep' = {
  name: 'assignAcrPull-app'
  scope: resourceGroup(acrResourceGroup)
  params: {
    acrName: acrName
    principalId: AppServiceApp.identity.principalId
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
