// CardLab demo environment (DEC-3, DEC-10, DEC-11): App Service plan F1 (Linux) + web app, Azure SQL logical
// server (Entra-only auth) + free-offer serverless database + Azure-services firewall rule. Nothing else is
// declared, so every resource is on a free tier (NFR-3).
// Deliberately no `appSettings` on the site: deploy.yml merges the non-secret settings with
// `az webapp config appsettings set`, so the human-set CardLab__LookupKey survives every redeploy.
targetScope = 'resourceGroup'

@description('Azure region for every resource.')
param location string = resourceGroup().location

@description('Globally unique web app name (also the managed identity name used in the SQL user T-SQL).')
param webAppName string

@description('Globally unique Azure SQL logical server name.')
param sqlServerName string

@description('UPN of the Entra user who administers the SQL server (design DEC-10: the human's identity).')
param sqlAdminLogin string

@description('Object id of the Entra identity that administers the SQL server.')
param sqlAdminObjectId string

@description('Tenant id of the Entra directory.')
param tenantId string = subscription().tenantId

@description('Database name.')
param databaseName string = 'cardlab'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${webAppName}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  properties: {
    reserved: true
  }
}

resource site 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      // Matches the app's .NET 10; the self-contained publish does not depend on it.
      linuxFxVersion: 'DOTNETCORE|10.0'
      appCommandLine: '/home/site/wwwroot/CardLab.Api'
      alwaysOn: false
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
    }
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: sqlAdminLogin
      sid: sqlAdminObjectId
      tenantId: tenantId
      azureADOnlyAuthentication: true
    }
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 60
    minCapacity: json('0.5')
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output webAppHostName string = site.properties.defaultHostName
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseResourceName string = database.name
