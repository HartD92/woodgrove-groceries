// =============================================================================
// Woodgrove Groceries — Azure Infrastructure Orchestrator
// Scope: subscription  (creates the resource group, then deploys all modules)
//
// azd-friendly deployment:
//   az deployment sub create `
//     --location westus2 `
//     --template-file infra/main.bicep `
//     --parameters infra/main.bicepparam
//
// Resource Group is created by this template; no manual az group create needed.
// =============================================================================

targetScope = 'subscription'

// ============================================================
// PARAMETERS
// ============================================================

@minLength(1)
@maxLength(16)
@description('Short environment identifier used in all resource names (e.g. dev, staging, prod)')
param environmentName string

@description('Azure region for all resources')
param location string = 'westus2'

@description('App Service Plan SKU name (default S1 = Standard 1 core)')
param appServicePlanSku string = 'S1'

// --- Entra External ID (non-secret configuration) ---

@description('Entra External ID (CIAM) tenant ID')
param tenantId string

@description('App registration client ID — woodgrove-groceries web app')
param webClientId string

@description('App registration client ID — woodgrove-groceries-api')
param apiClientId string

@description('App registration client ID — graph-middleware')
param graphClientId string

@description('App registration client ID — auth-api (custom authentication extension)')
param authClientId string

@description('Primary public domain for the web app (e.g. woodgrovedemo.com)')
param webDomain string = 'woodgrovedemo.com'

@description('Entra authority URL — e.g. https://<tenant>.ciamlogin.com/<tenantId>/v2.0')
param entraAuthorityUrl string

@description('Cloudflare Zone ID (non-secret; used for DNS automation app setting)')
param cloudflareZoneId string = ''

@description('Certificate thumbprints to load into Windows cert store (* = all certs available to the app)')
param websiteLoadCertificates string = '*'

@description('ACS data residency location')
param acsDataLocation string = 'United States'

@description('Additional tags applied to all resources')
param tags object = {}

// --- Entra provisioning toggle ---

@description('''
When true, Bicep provisions the four Entra app registrations via the Microsoft
Graph extension and uses their output appIds for app settings. This mode is
retained only for backward compatibility; sub-scoped deployments authenticate to
the subscription tenant, so the Graph extension cannot target a separate ExtID
tenant.

When false (default), the *ClientId params below are used directly. CI/CD uses
this mode after the ExtID app registrations are provisioned by Azure CLI.
''')
param provisionEntraApps bool = false

// ============================================================
// DERIVED NAMES  —  {type}-woodgrove-{component}-{env}
// ============================================================

// 6-char hash unique per (subscription + environment) — appended where global uniqueness is required
var uniqueSuffix = take(uniqueString(subscription().subscriptionId, environmentName), 6)

var rgName           = 'rg-woodgrove-${environmentName}'
var planName         = 'plan-woodgrove-main-${environmentName}'
var webAppName       = 'app-woodgrove-web-${environmentName}-${uniqueSuffix}'
var apiAppName       = 'app-woodgrove-api-${environmentName}-${uniqueSuffix}'
var graphAppName     = 'app-woodgrove-graph-${environmentName}-${uniqueSuffix}'
var authAppName      = 'app-woodgrove-auth-${environmentName}-${uniqueSuffix}'

// Key Vault names: 3-24 chars, globally unique
// 'kv-wg-<env>-<suffix>' keeps it within 24 chars even for long env names
var kvName           = 'kv-wg-${environmentName}-${uniqueSuffix}'

// kvBaseUri is deterministic from the name — used to build KV references before KV is deployed.
// environment().suffixes.keyvaultDns returns '.vault.azure.net' (leading dot included),
// ensuring compatibility with sovereign clouds.
var kvBaseUri        = 'https://${kvName}${environment().suffixes.keyvaultDns}/'

var logWorkspaceName = 'log-woodgrove-${environmentName}'
var appInsightsName  = 'appi-woodgrove-${environmentName}'
var acsName          = 'acs-woodgrove-${environmentName}-${uniqueSuffix}'
var emailSvcName     = 'email-woodgrove-${environmentName}-${uniqueSuffix}'

var allTags = union(tags, {
  environment: environmentName
  project: 'woodgrove-groceries'
  managedBy: 'bicep'
  SecurityControl: 'Ignore'
})

// ============================================================
// ENTRA APP REGISTRATIONS  (DEPRECATED Microsoft Graph extension path)
// Retained for backward compatibility only. In the supported two-tenant CI/CD
// path, the ExtID app registrations are provisioned before this deployment and
// provisionEntraApps=false passes their client IDs in as parameters.
// ============================================================

module entraApps 'modules/entraApps.bicep' = if (provisionEntraApps) {
  name: 'entraApps'
  // The MS Graph extension resources have no ARM scope — they deploy to the
  // Entra tenant via the Graph API.  We still provide `scope: rg` so the ARM
  // deployment engine has a valid resource group context.  The Graph resources
  // inside the module are unaffected by this ARM scope.
  scope: rg
  params: {
    environmentName: environmentName
    webDomain: webDomain
    // Deterministic hostnames resolve before web apps are deployed — no circular dependency.
    webAppHostName:  '${webAppName}.azurewebsites.net'
    authAppHostName: '${authAppName}.azurewebsites.net'
  }
}

// Resolved client IDs — use Entra module outputs when provisionEntraApps=true,
// fall back to input parameters when false.
// ?? '' guards the BCP318 null-check; the outer ternary ensures the fallback
// is the input param whenever the module is not deployed.
var resolvedWebClientId   = provisionEntraApps ? (entraApps.outputs.webClientId   ?? '') : webClientId
var resolvedApiClientId   = provisionEntraApps ? (entraApps.outputs.apiClientId   ?? '') : apiClientId
var resolvedGraphClientId = provisionEntraApps ? (entraApps.outputs.graphClientId ?? '') : graphClientId
var resolvedAuthClientId  = provisionEntraApps ? (entraApps.outputs.authClientId  ?? '') : authClientId

// ============================================================
// KEY VAULT SECRET REFERENCES
// All secret app settings use this pattern; actual secrets are seeded post-deploy.
// Versionless URI (trailing /) always resolves to the current secret version.
// ============================================================

var kvRefAppInsights  = '@Microsoft.KeyVault(SecretUri=${kvBaseUri}secrets/appinsights-connection-string/)'
var kvRefAcsConn      = '@Microsoft.KeyVault(SecretUri=${kvBaseUri}secrets/acs-connection-string/)'
var kvRefWebSecret    = '@Microsoft.KeyVault(SecretUri=${kvBaseUri}secrets/web-client-secret/)'
var kvRefCloudflare   = '@Microsoft.KeyVault(SecretUri=${kvBaseUri}secrets/cloudflare-api-secret/)'

// ============================================================
// RESOURCE GROUP
// ============================================================

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: rgName
  location: location
  tags: allTags
}

// ============================================================
// MONITORING  (Log Analytics + Application Insights)
// No external dependencies — deploy first.
// ============================================================

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  scope: rg
  params: {
    workspaceName: logWorkspaceName
    appInsightsName: appInsightsName
    location: location
    tags: allTags
  }
}

// ============================================================
// APP SERVICE PLAN  (Windows, shared by all four web apps)
// ============================================================

module plan 'modules/appServicePlan.bicep' = {
  name: 'appServicePlan'
  scope: rg
  params: {
    name: planName
    location: location
    sku: appServicePlanSku
    tags: allTags
  }
}

// ============================================================
// WEB APPS
// Deployed before Key Vault so their system-assigned managed identity
// principalIds are available for RBAC assignment in the KV module.
// App settings that reference KV secrets use the deterministic kvBaseUri;
// references resolve at runtime once KV is seeded (see README).
// ============================================================

// --- web:  woodgrove-groceries / woodgrovedemo.com ---
module webApp 'modules/webApp.bicep' = {
  name: 'webApp'
  scope: rg
  params: {
    name: webAppName
    location: location
    appServicePlanId: plan.outputs.id
    netFrameworkVersion: 'v8.0'
    websiteLoadCertificates: websiteLoadCertificates
    tags: allTags
    appSettings: [
      { name: 'AzureAd__TenantId',                            value: tenantId }
      { name: 'AzureAd__ClientId',                            value: resolvedWebClientId }
      { name: 'AzureAd__ClientSecret',                        value: kvRefWebSecret }
      { name: 'AzureAd__Domain',                              value: webDomain }
      { name: 'AzureAd__Authority',                           value: entraAuthorityUrl }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING',         value: kvRefAppInsights }
      { name: 'Api__BaseUrl',                                  value: 'https://${apiAppName}.azurewebsites.net' }
      { name: 'GraphMiddleware__BaseUrl',                      value: 'https://${graphAppName}.azurewebsites.net' }
      { name: 'AuthApi__BaseUrl',                              value: 'https://${authAppName}.azurewebsites.net' }
      { name: 'AzureCommunicationServices__ConnectionString',  value: kvRefAcsConn }
      { name: 'Cloudflare__ZoneId',                            value: cloudflareZoneId }
      { name: 'Cloudflare__ApiSecret',                         value: kvRefCloudflare }
    ]
  }
}

// --- api:  woodgrove-groceries-api  (api.woodgrovedemo.com) ---
module apiApp 'modules/webApp.bicep' = {
  name: 'apiApp'
  scope: rg
  params: {
    name: apiAppName
    location: location
    appServicePlanId: plan.outputs.id
    netFrameworkVersion: 'v8.0'
    websiteLoadCertificates: websiteLoadCertificates
    tags: allTags
    appSettings: [
      { name: 'AzureAd__TenantId',                            value: tenantId }
      { name: 'AzureAd__ClientId',                            value: resolvedApiClientId }
      { name: 'AzureAd__Authority',                           value: entraAuthorityUrl }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING',         value: kvRefAppInsights }
      { name: 'AzureCommunicationServices__ConnectionString',  value: kvRefAcsConn }
    ]
  }
}

// --- graph:  woodgrove-groceries-graph-middleware  (graph-middleware.woodgrovedemo.com) ---
module graphApp 'modules/webApp.bicep' = {
  name: 'graphApp'
  scope: rg
  params: {
    name: graphAppName
    location: location
    appServicePlanId: plan.outputs.id
    netFrameworkVersion: 'v8.0'
    websiteLoadCertificates: websiteLoadCertificates
    tags: allTags
    appSettings: [
      { name: 'AzureAd__TenantId',          value: tenantId }
      { name: 'AzureAd__ClientId',          value: resolvedGraphClientId }
      { name: 'AzureAd__Authority',         value: entraAuthorityUrl }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: kvRefAppInsights }
    ]
  }
}

// --- auth:  woodgrove-auth-api  (auth-api.woodgrovedemo.com)
// *** STABLE PUBLIC HTTPS ENDPOINT REQUIRED ***
// Entra External ID registers this app as a custom authentication extension.
// The callback URL is: https://<authAppName>.azurewebsites.net/api/CustomAuthenticationExtension
// That exact URL must be registered in the Entra portal — see README for manual steps.
module authApp 'modules/webApp.bicep' = {
  name: 'authApp'
  scope: rg
  params: {
    name: authAppName
    location: location
    appServicePlanId: plan.outputs.id
    netFrameworkVersion: 'v8.0'
    websiteLoadCertificates: websiteLoadCertificates
    tags: allTags
    appSettings: [
      { name: 'AzureAd__TenantId',          value: tenantId }
      { name: 'AzureAd__ClientId',          value: resolvedAuthClientId }
      { name: 'AzureAd__Authority',         value: entraAuthorityUrl }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: kvRefAppInsights }
    ]
  }
}

// ============================================================
// KEY VAULT
// Deployed AFTER web apps: Bicep resolves the implicit dependency via
// the principalId output references below, so all four managed identities
// exist before role assignments are created.
// ============================================================

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  scope: rg
  params: {
    name: kvName
    location: location
    tags: allTags
    appPrincipalIds: [
      webApp.outputs.principalId
      apiApp.outputs.principalId
      graphApp.outputs.principalId
      authApp.outputs.principalId
    ]
    deployerPrincipalId: deployer().objectId
  }
}

// ============================================================
// AZURE COMMUNICATION SERVICES  (email OTP)
// ============================================================

module acs 'modules/communicationServices.bicep' = {
  name: 'communicationServices'
  scope: rg
  params: {
    name: acsName
    emailServiceName: emailSvcName
    dataLocation: acsDataLocation
    tags: allTags
  }
}

// ============================================================
// OUTPUTS
// ============================================================

output resourceGroupName  string = rg.name
output webAppHostName     string = webApp.outputs.defaultHostName
output apiAppHostName     string = apiApp.outputs.defaultHostName
output graphAppHostName   string = graphApp.outputs.defaultHostName
output authAppHostName    string = authApp.outputs.defaultHostName
output keyVaultName       string = keyVault.outputs.name
output keyVaultUri        string = keyVault.outputs.uri
output appInsightsName    string = monitoring.outputs.appInsightsName
output acsResourceName    string = acs.outputs.name

// Client IDs — either from Entra module (provisionEntraApps=true) or input params
output resolvedWebClientId   string = resolvedWebClientId
output resolvedApiClientId   string = resolvedApiClientId
output resolvedGraphClientId string = resolvedGraphClientId
output resolvedAuthClientId  string = resolvedAuthClientId
