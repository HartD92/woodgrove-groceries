@description('Web app resource name — must be globally unique; becomes <name>.azurewebsites.net')
param name string

@description('Azure region')
param location string

@description('Resource ID of the hosting App Service Plan')
param appServicePlanId string

@description('.NET runtime version loaded by the Windows host for modern .NET apps (e.g. v10.0, v8.0)')
param netFrameworkVersion string = 'v10.0'

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

@description('Custom hostname to bind to this App Service. Leave empty to skip custom-domain binding.')
param customHostName string = ''

@description('When true, bind an App Service Managed Certificate to customHostName. Requires a prior deploy with customHostName bound and DNS validated.')
param enableManagedCertificate bool = false

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

var hasCustomHostName = !empty(customHostName)
var managedCertificateName = '${customHostName}-${webApp.name}'

resource managedCertificate 'Microsoft.Web/certificates@2023-12-01' = if (hasCustomHostName && enableManagedCertificate) {
  name: managedCertificateName
  location: location
  tags: tags
  properties: {
    serverFarmId: appServicePlanId
    canonicalName: customHostName
  }
}

resource customHostNameBinding 'Microsoft.Web/sites/hostNameBindings@2023-12-01' = if (hasCustomHostName) {
  parent: webApp
  name: customHostName
  properties: {
    siteName: name
    hostNameType: 'Verified'
    sslState: enableManagedCertificate ? 'SniEnabled' : 'Disabled'
    thumbprint: enableManagedCertificate ? managedCertificate!.properties.thumbprint : null
  }
}

output id string = webApp.id
output name string = webApp.name
output principalId string = webApp.identity.principalId
output defaultHostName string = webApp.properties.defaultHostName
output customHostName string = hasCustomHostName ? customHostNameBinding.name : ''
