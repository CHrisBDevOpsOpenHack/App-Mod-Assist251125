@description('Location for resources')
param location string = 'uksouth'

@description('SQL Administrator Login (Entra ID User Principal Name)')
param adminLogin string

@description('SQL Administrator Object ID (Entra ID)')
param adminObjectId string

@description('Deploy GenAI resources')
param deployGenAI bool = false

// Generate unique suffix for resource names
var uniqueSuffix = toLower(uniqueString(resourceGroup().id))
var baseName = 'expensemgmt-${uniqueSuffix}'

// Get current day-hour-minute for managed identity name
var managedIdentityName = 'mid-appmodassist-${uniqueSuffix}'

// Deploy App Service and Managed Identity
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    baseName: baseName
    managedIdentityName: managedIdentityName
  }
}

// Deploy Azure SQL
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    baseName: baseName
    adminLogin: adminLogin
    adminObjectId: adminObjectId
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Conditionally deploy GenAI resources
module genai 'genai.bicep' = if (deployGenAI) {
  name: 'genaiDeployment'
  params: {
    location: 'swedencentral'
    baseName: baseName
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Outputs
output appServiceName string = appService.outputs.appServiceName
output appServiceHostName string = appService.outputs.appServiceHostName
output managedIdentityId string = appService.outputs.managedIdentityId
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = appService.outputs.managedIdentityPrincipalId
output managedIdentityName string = appService.outputs.managedIdentityName
output sqlServerName string = azureSql.outputs.sqlServerName
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output databaseName string = azureSql.outputs.databaseName

// Conditional GenAI outputs
output openAIEndpoint string = deployGenAI ? genai.outputs.openAIEndpoint : ''
output openAIName string = deployGenAI ? genai.outputs.openAIName : ''
output openAIModelName string = deployGenAI ? genai.outputs.openAIModelName : ''
output searchEndpoint string = deployGenAI ? genai.outputs.searchEndpoint : ''
output searchName string = deployGenAI ? genai.outputs.searchName : ''
