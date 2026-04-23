/// containerRegistry.bicep — Provisions an Azure Container Registry for storing
/// PromptBank Docker images. Uses Basic SKU for dev and Standard for prod.

@description('Name of the Container Registry (alphanumeric only, 5-50 chars).')
param name string

@description('Azure region for the registry.')
param location string

@description('SKU tier: Basic for dev, Standard for prod.')
@allowed(['Basic', 'Standard'])
param sku string

resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: name
  location: location
  sku: {
    name: sku
  }
  properties: {
    adminUserEnabled: false
  }
}

@description('Login server hostname of the registry (e.g. crpromptbankdev.azurecr.io).')
output loginServer string = acr.properties.loginServer

@description('Resource ID of the registry — used to assign acrPull role to the Web App identity.')
output id string = acr.id

@description('Name of the registry resource.')
output name string = acr.name
