@description('App Service Plan resource name')
param name string

@description('Azure region')
param location string

@description('App Service Plan SKU name (e.g. P1v3, P2v3, S1)')
param sku string = 'P1v3'

@description('Resource tags')
param tags object = {}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: sku
  }
  kind: 'windows'
  properties: {
    reserved: false // Windows (not Linux)
  }
}

output id string = appServicePlan.id
output name string = appServicePlan.name
