@description('Key Vault resource name (3–24 chars, globally unique, alphanumeric + hyphens)')
param name string

@description('Azure region')
param location string

@description('Resource tags')
param tags object = {}

@description('Object IDs (principalId) of system-assigned managed identities to grant KV access')
param appPrincipalIds array = []

@description('Object ID of the CI/CD deploying service principal to grant Key Vault Secrets Officer (read+write secrets). Empty string disables.')
param deployerPrincipalId string = ''

// ------------------------------------------------------------------
// Built-in Azure RBAC role definition IDs (same in every tenant/sub)
// ------------------------------------------------------------------
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
var kvCertsUserRoleId   = 'db79e9a7-68ee-4b58-9aeb-b90e7c24fcba' // Key Vault Certificate User
var kvSecretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7' // Key Vault Secrets Officer

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true     // Access policies disabled; use RBAC exclusively
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enabledForDeployment: false
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: false
    publicNetworkAccess: 'Enabled'    // Tighten with private endpoint for production
  }
}

// Grant Key Vault Secrets User to each app's managed identity
resource secretsUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in appPrincipalIds: {
    name: guid(keyVault.id, principalId, kvSecretsUserRoleId)
    scope: keyVault
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

// Grant Key Vault Certificate User to each app's managed identity
resource certsUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in appPrincipalIds: {
    name: guid(keyVault.id, principalId, kvCertsUserRoleId)
    scope: keyVault
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvCertsUserRoleId)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

resource deployerSecretsOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployerPrincipalId)) {
  name: guid(keyVault.id, deployerPrincipalId, kvSecretsOfficerRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsOfficerRoleId)
    principalId: deployerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output id string = keyVault.id
output name string = keyVault.name
output uri string = keyVault.properties.vaultUri
