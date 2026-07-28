@description('Key Vault name')
param keyVaultName string

@description('Secret name')
param secretName string

@description('Secret value')
@secure()
param secretValue string

resource keyVaultExisting 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource secret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVaultExisting
  name: secretName
  properties: {
    value: secretValue
  }
}
