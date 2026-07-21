@description('Web app resource name — must be globally unique; becomes <name>.azurewebsites.net')
param name string

@description('Azure region')
param location string

@description('Resource ID of the hosting App Service Plan')
param appServicePlanId string

@description('.NET Framework version loaded by the Windows host (e.g. v8.0, v6.0)')
param netFrameworkVersion string = 'v8.0'

@description('''
App settings array: [ { name: string, value: string }, ... ]
Secrets should use Key Vault references:
  @Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/<name>/)
''')
param appSettings array = []

@description('Certificate thumbprints to load into the Windows cert store. Use * for all certs available to this app.')
param websiteLoadCertificates string = '*'

@description('Resource tags')
param tags object = {}

// Settings injected by this module for every app.
// WEBSITE_LOAD_CERTIFICATES enables Windows cert-store loading so code can call
//   X509Store / CertificateRequest.  ANCM in-process is configured at the app level
//   via web.config <aspNetCore hostingModel="inprocess"/>.
var platformSettings = [
  { name: 'WEBSITE_LOAD_CERTIFICATES', value: websiteLoadCertificates }
  { name: 'ApplicationInsightsAgent_EXTENSION_VERSION', value: '~3' }
  { name: 'XDT_MicrosoftApplicationInsights_Mode', value: 'Recommended' }
]

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      netFrameworkVersion: netFrameworkVersion
      use32BitWorkerProcess: false       // 64-bit worker
      managedPipelineMode: 'Integrated'  // Required for ANCM in-process
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [...platformSettings, ...appSettings]
    }
  }
}

output id string = webApp.id
output name string = webApp.name
output principalId string = webApp.identity.principalId
output defaultHostName string = webApp.properties.defaultHostName
