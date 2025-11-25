@description('Location for the SQL Server')
param location string = 'uksouth'

@description('Base name for resources')
param baseName string

@description('SQL Administrator Login (Entra ID)')
param adminLogin string

@description('SQL Administrator Object ID (Entra ID)')
param adminObjectId string

@description('Managed Identity Principal ID for database access')
param managedIdentityPrincipalId string

// Create SQL Server with Entra ID only authentication
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: 'sql-${baseName}'
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: adminLogin
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

// Allow Azure Services to access SQL Server
resource sqlServerFirewallAzure 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Create Northwind Database
resource sqlDatabase 'Microsoft.Sql/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: 'Northwind'
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
  }
}

output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = sqlDatabase.name
