/// main.bicep — Orchestration entry point for Prompt Bank Azure infrastructure.
/// Accepts an environment parameter ('dev' or 'prod') and wires together three
/// modules: App Service Plan, Azure Container Registry, and App Service.

@description('Target deployment environment. Drives resource naming and SKU selection.')
@allowed(['dev', 'prod'])
param environment string

@description('Azure region for all resources. Defaults to uksouth.')
param location string = 'uksouth'

@description('Docker image tag to deploy to the App Service (e.g. sha-abc1234 or latest).')
param imageTag string = 'latest'

var appPlanSku = environment == 'prod' ? 'B2' : 'B1'
var acrSku = environment == 'prod' ? 'Standard' : 'Basic'
var aspnetEnvironment = environment == 'prod' ? 'Production' : 'Development'

// Resource naming — all follow the pattern described in FR-5
var resourceNames = {
  appServicePlan: 'asp-promptbank-${environment}'
  appService: 'app-promptbank-${environment}'
  // ACR names must be globally unique, alphanumeric only, 5-50 chars
  containerRegistry: 'crpromptbank${environment}'
}

module asp 'modules/appServicePlan.bicep' = {
  name: 'appServicePlanDeploy'
  params: {
    name: resourceNames.appServicePlan
    location: location
    sku: appPlanSku
  }
}

module acr 'modules/containerRegistry.bicep' = {
  name: 'containerRegistryDeploy'
  params: {
    name: resourceNames.containerRegistry
    location: location
    sku: acrSku
  }
}

module app 'modules/appService.bicep' = {
  name: 'appServiceDeploy'
  params: {
    name: resourceNames.appService
    location: location
    appServicePlanId: asp.outputs.id
    aspnetEnvironment: aspnetEnvironment
    acrLoginServer: acr.outputs.loginServer
    acrId: acr.outputs.id
    imageTag: imageTag
  }
}

@description('Name of the Web App — used by the app deployment workflow.')
output appServiceName string = app.outputs.name

@description('Default hostname of the Web App.')
output appServiceUrl string = app.outputs.defaultHostName

@description('Login server of the Container Registry (e.g. crpromptbankdev.azurecr.io).')
output acrLoginServer string = acr.outputs.loginServer

@description('Name of the Container Registry resource.')
output acrName string = acr.outputs.name
