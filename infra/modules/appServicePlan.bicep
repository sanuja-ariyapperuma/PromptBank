/// App Service Plan module — provisions a Linux App Service Plan with a
/// configurable SKU (B1 for dev, B2 for prod).

@description('Name of the App Service Plan.')
param name string

@description('Azure region for the App Service Plan.')
param location string

@description('SKU name for the plan (e.g. B1, B2).')
param sku string

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: name
  location: location
  kind: 'linux'
  sku: {
    name: sku
  }
  properties: {
    reserved: true // Linux hosting
  }
}

@description('Resource ID of the App Service Plan.')
output id string = appServicePlan.id

@description('Name of the provisioned App Service Plan.')
output name string = appServicePlan.name
