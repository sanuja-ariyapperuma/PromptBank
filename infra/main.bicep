/// main.bicep — Orchestration entry point for Prompt Bank Azure infrastructure.
/// Accepts an environment parameter ('dev' or 'prod') and wires together two
/// modules: App Service Plan and App Service (with SQLite on /home filesystem).

@description('Target deployment environment. Drives resource naming and SKU selection.')
@allowed(['dev', 'prod'])
param environment string

@description('Azure region for all resources. Defaults to uksouth.')
param location string = 'uksouth'

var appPlanSku = environment == 'prod' ? 'B2' : 'B1'
var aspnetEnvironment = environment == 'prod' ? 'Production' : 'Development'

// Resource naming — all follow the pattern described in FR-5
var resourceNames = {
  appServicePlan: 'asp-promptbank-${environment}'
  appService: 'app-promptbank-${environment}'
}

module asp 'modules/appServicePlan.bicep' = {
  name: 'appServicePlanDeploy'
  params: {
    name: resourceNames.appServicePlan
    location: location
    sku: appPlanSku
  }
}

module app 'modules/appService.bicep' = {
  name: 'appServiceDeploy'
  params: {
    name: resourceNames.appService
    location: location
    appServicePlanId: asp.outputs.id
    aspnetEnvironment: aspnetEnvironment
  }
}

@description('Name of the Web App — used by the app deployment workflow for az webapp deploy.')
output appServiceName string = app.outputs.name

@description('Default hostname of the Web App.')
output appServiceUrl string = app.outputs.defaultHostName
