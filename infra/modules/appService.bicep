/// App Service module — provisions a Linux Web App running the PromptBank Docker
/// image pulled from Azure Container Registry. SQLite database is stored on the
/// persistent /home filesystem (backed by Azure Files).

@description('Name of the Web App.')
param name string

@description('Azure region for the Web App.')
param location string

@description('Resource ID of the App Service Plan.')
param appServicePlanId string

@description('Value for the ASPNETCORE_ENVIRONMENT app setting (Development or Production).')
param aspnetEnvironment string

@description('Login server of the Azure Container Registry (e.g. crpromptbankdev.azurecr.io).')
param acrLoginServer string

@description('Resource ID of the Azure Container Registry — used to grant acrPull to the managed identity.')
param acrId string

@description('Docker image tag to deploy (e.g. sha-abc1234 or latest).')
param imageTag string = 'latest'

// Built-in acrPull role definition ID (fixed across all Azure tenants)
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

// SQLite database stored on the persistent /home mount — survives restarts
var sqliteConnectionString = 'Data Source=/home/data/promptbank.db'

resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: name
  location: location
  identity: {
    // System-assigned managed identity allows the App Service to pull images
    // from ACR without storing any registry credentials.
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${acrLoginServer}/promptbank:${imageTag}'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: aspnetEnvironment
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: sqliteConnectionString
        }
        {
          // Pull images from ACR using the system-assigned managed identity
          // instead of admin credentials.
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acrLoginServer}'
        }
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'true'
        }
        {
          // ONNX model loading + embedding generation on first boot exceeds the
          // default 230s container start limit. 600s gives enough headroom on B1/B2.
          name: 'WEBSITES_CONTAINER_START_TIME_LIMIT'
          value: '600'
        }
      ]
    }
  }
}

// Grant the Web App's managed identity the acrPull role on the registry
resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(webApp.id, acrId, acrPullRoleId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

@description('Name of the provisioned Web App.')
output name string = webApp.name

@description('Default hostname of the Web App (e.g. app-promptbank-dev.azurewebsites.net).')
output defaultHostName string = webApp.properties.defaultHostName
