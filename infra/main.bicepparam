// =============================================================================
// Woodgrove Groceries — Bicep Parameter File
// Target:  infra/main.bicep
//
// IMPORTANT: This file contains NO secrets and NO real GUIDs.
//   All placeholder values are clearly labelled with angle brackets.
//   Secrets (connection strings, client secrets) are seeded into Key Vault
//   post-deploy and are NEVER placed here.  See infra/README.md.
// =============================================================================

using './main.bicep'

// --- Environment ---
param environmentName = 'dev'
param location        = 'eastus2'
param appServicePlanSku = 'P1v3'

// --- Entra External ID tenant (no secrets — from Azure portal > Entra > Overview) ---
param tenantId = '<your-entra-external-id-tenant-id>'

// --- App registration Client IDs (non-secret — visible in Azure portal) ---
// Register each app in Entra External ID before deploying; copy the Application (client) ID.
param webClientId   = '<web-app-client-id>'          // woodgrove-groceries app reg
param apiClientId   = '<api-app-client-id>'           // woodgrove-groceries-api app reg
param graphClientId = '<graph-middleware-client-id>'  // graph-middleware app reg
param authClientId  = '<auth-api-client-id>'          // auth-api app reg (custom auth ext)

// --- Domain & authority ---
param webDomain        = 'woodgrovedemo.com'
param entraAuthorityUrl = 'https://<tenant-subdomain>.ciamlogin.com/<your-entra-external-id-tenant-id>/v2.0'

// --- Cloudflare (non-secret zone identifier) ---
param cloudflareZoneId = '<cloudflare-zone-id>'

// --- Certificate loading ---
// '*' loads all certs available to the app into the Windows cert store.
// Replace with a comma-separated thumbprint list to restrict which certs are loaded.
param websiteLoadCertificates = '*'

// --- ACS data residency ---
param acsDataLocation = 'United States'

// --- Tags ---
param tags = {
  owner: '<team-or-owner-alias>'
  costCenter: '<cost-center-code>'
}
