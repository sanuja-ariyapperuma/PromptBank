/// App Service module — provisions a Linux Web App with SQLite database
/// stored on the persistent /home filesystem (backed by Azure Files).

@description('Name of the Web App.')
param name string

@description('Azure region for the Web App.')
param location string

@description('Resource ID of the App Service Plan.')
param appServicePlanId string

@description('Value for the ASPNETCORE_ENVIRONMENT app setting (Development or Production).')
param aspnetEnvironment string

// SQLite database stored on the persistent /home mount — survives restarts
var sqliteConnectionString = 'Data Source=/home/data/promptbank.db'

resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: name
  location: location
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
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
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
  }
}

@description('Name of the provisioned Web App.')
output name string = webApp.name

@description('Default hostname of the Web App (e.g. app-promptbank-dev.azurewebsites.net).')
output defaultHostName string = webApp.properties.defaultHostName
