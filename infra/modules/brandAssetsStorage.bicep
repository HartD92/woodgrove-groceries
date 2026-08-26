@description('Brand-assets Storage Account name (3-24 chars, lowercase alphanumeric)')
param name string

@description('Azure region')
param location string

@description('Blob container name for public-read brand assets')
param containerName string = 'brand-assets'

@description('Resource tags')
param tags object = {}

@description('Object IDs (principalId) of managed identities that need RBAC read access to brand assets')
param readerPrincipalIds array = []

@description('Object ID of the CI/CD deploying service principal to grant Blob Data Contributor for asset uploads. Empty string disables.')
param deployerPrincipalId string = ''

var blobDataReaderRoleId = '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'      // Storage Blob Data Reader
var blobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe' // Storage Blob Data Contributor

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    // Required so this specific container can expose anonymous blob reads for
    // login-page images. Listing is still disabled at the container level.
    allowBlobPublicAccess: true
    allowCrossTenantReplication: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Enabled'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: containerName
  properties: {
    // Anonymous GET is allowed for individual blobs only; container enumeration
    // remains disabled.
    publicAccess: 'Blob'
  }
}

resource blobDataReaderAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in readerPrincipalIds: {
    name: guid(container.id, principalId, blobDataReaderRoleId)
    scope: container
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataReaderRoleId)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

resource deployerBlobDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployerPrincipalId)) {
  name: guid(container.id, deployerPrincipalId, blobDataContributorRoleId)
  scope: container
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalId: deployerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output storageAccountName string = storageAccount.name
output containerName string = container.name
output containerBaseUrl string = 'https://${storageAccount.name}.blob.${environment().suffixes.storage}/${container.name}/'
