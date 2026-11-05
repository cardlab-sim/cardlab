using './main.bicep'

// Identifiers only, read from the deploy job's environment (repo variables); nothing is committed.
// Defaults let `az bicep build-params` succeed in CI where the variables are not set.
param webAppName = readEnvironmentVariable('AZURE_WEBAPP_NAME', 'cardlab-demo')
param sqlServerName = readEnvironmentVariable('AZURE_SQL_SERVER_NAME', 'cardlab-demo-sql')
param sqlAdminLogin = readEnvironmentVariable('AZURE_SQL_ADMIN_LOGIN', 'sql-admin')
param sqlAdminObjectId = readEnvironmentVariable('AZURE_SQL_ADMIN_OBJECT_ID', '00000000-0000-0000-0000-000000000000')
