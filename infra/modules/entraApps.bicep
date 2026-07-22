// =============================================================================
// Woodgrove Groceries — Entra App Registrations + Service Principals
//
// DEPRECATED FOR DEPLOYMENT. Retained as the authoritative specification for
// the four CIAM app registrations that the GitHub Actions CLI provisioning job
// must create. The Microsoft Graph Bicep extension targets the deployment
// principal's tenant; subscription deployments authenticate to the workforce
// tenant, so this module cannot provision into a separate ExtID/CIAM tenant.
//
// Uses the Microsoft Graph Bicep extension (v1.0) to provision all four
// Entra External ID app registrations and their service principals.
//
// IMPORTANT LIMITATIONS — read before deploying:
//
//   1. customAuthenticationExtensions: NOT available as a Bicep resource type
//      in v1.0 (or beta) as of August 2025.  The custom auth extension must be
//      registered out-of-band after deployment.  See README § Custom Auth Ext.
//
//   2. Client secret value: passwordCredentials is intentionally omitted from
//      all app resources below — the Graph API rejects direct writes and
//      requires the addPassword action instead.  The deploy workflow runs
//      `az ad app credential reset --append` automatically and pipes the
//      returned secret value into Key Vault.  No manual portal step required.
//
//   3. Admin consent for Graph app roles (graph-middleware): granting consent
//      requires AppRoleAssignment.ReadWrite.All + DelegatedPermissionGrant.
//      ReadWrite.All on the deployer identity.  If not granted, the
//      requiredResourceAccess declarations are created but consent must be
//      applied manually in the Entra portal.  See README § Permissions.
//
//   4. Idempotency: The `uniqueName` property is the stable key the Graph
//      extension uses to match existing resources across deployments.
//      Change uniqueName only if you intentionally want a new registration.
// =============================================================================

extension microsoftGraphV1

// ============================================================
// PARAMETERS
// ============================================================

@description('Short environment identifier — drives uniqueName suffix so dev/staging/prod get separate registrations')
param environmentName string

@description('Primary public domain for the web app (e.g. woodgrovedemo.com)')
param webDomain string = 'woodgrovedemo.com'

@description('Default hostname of the web App Service (used for redirect URIs)')
param webAppHostName string

@description('Default hostname of the auth-api App Service (custom auth extension callback host)')
param authAppHostName string

// ============================================================
// DETERMINISTIC IDENTIFIERS
// Use guid() for stable scope / role IDs so re-deployments are idempotent.
// ============================================================

// Delegated scope ID exposed by the API app ("access_as_user")
// deterministic: same value for the same environmentName across all deployments
var apiAccessAsUserScopeId = guid(environmentName, 'woodgrove-api', 'access_as_user')

// ============================================================
// WELL-KNOWN CONSTANTS
// ============================================================

// Microsoft Graph service principal appId (same in every tenant)
var msGraphAppId = '00000003-0000-0000-c000-000000000000'

// VERIFY: MS Graph application permission IDs below are the well-known stable
// values documented at https://learn.microsoft.com/en-us/graph/permissions-reference
// but confirm against current docs before deploying.
var graphUserReadWriteAllId = '741f803b-c850-494e-b5df-cde7c675a1ca'   // User.ReadWrite.All (application)
var graphGroupReadWriteAllId = '62a82d76-70ea-41e2-9197-370581804d09'  // Group.ReadWrite.All (application)
var graphDirectoryReadWriteAllId = '19dbc75e-c2e2-444c-a770-ec69d8559fc7' // Directory.ReadWrite.All (application)

// ============================================================
// APP 1 — woodgrove-groceries-api (defined FIRST; web app depends on its appId)
// ============================================================

resource apiApplication 'Microsoft.Graph/applications@v1.0' = {
  // uniqueName is the stable idempotency key for the Graph extension.
  uniqueName: 'woodgrove-groceries-api-${environmentName}'
  displayName: 'woodgrove-groceries-api (${environmentName})'

  // Expose an API with one delegated scope.
  // VERIFY: requestedAccessTokenVersion: 2 = v2.0 tokens (correct for CIAM/External ID).
  api: {
    requestedAccessTokenVersion: 2
    oauth2PermissionScopes: [
      {
        id: apiAccessAsUserScopeId
        adminConsentDisplayName: 'Access woodgrove-groceries-api as the signed-in user'
        adminConsentDescription: 'Allows the app to access woodgrove-groceries-api on behalf of the signed-in user.'
        userConsentDisplayName: 'Access woodgrove-groceries-api'
        userConsentDescription: 'Allows this app to access woodgrove-groceries-api on your behalf.'
        value: 'access_as_user'
        type: 'User'           // 'User' = user-consentable; 'Admin' = admin-only
        isEnabled: true
      }
    ]
    // VERIFY: preAuthorizedApplications — web app appId is needed here to
    // allow implicit consent without user prompt.  This is set AFTER the web
    // app is created (separate Portal or Graph API step) because of the
    // forward-reference limitation.  See README § Entra wiring.
  }

  // identifierUri: Use a stable human-readable URI rather than api://<appId>
  // to avoid the circular reference (appId isn't known until after creation).
  identifierUris: [
    'api://woodgrove-groceries-api-${environmentName}'
  ]

  // The API app is single-tenant (External ID tenant).
  signInAudience: 'AzureADMyOrg'
}

resource apiServicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: apiApplication.appId
  // tags for display in portal
  tags: [
    'woodgrove-groceries'
    environmentName
  ]
}

// ============================================================
// APP 2 — woodgrove-groceries (web / storefront)
// ============================================================

resource webApplication 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'woodgrove-groceries-web-${environmentName}'
  displayName: 'woodgrove-groceries (${environmentName})'

  signInAudience: 'AzureADMyOrg'

  // Web platform — redirect URIs include both the App Service hostname and the
  // custom domain.  Both are deterministic so no circular dependency.
  web: {
    redirectUris: [
      'https://${webAppHostName}/signin-oidc'
      'https://${webDomain}/signin-oidc'
    ]
    logoutUrl: 'https://${webAppHostName}/signout-oidc'
    implicitGrantSettings: {
      // ID tokens MUST be enabled for the confidential hybrid OIDC flow
      // David uses (SWA/App Service confidential hybrid).
      enableIdTokenIssuance: true
      enableAccessTokenIssuance: false
    }
  }

  // NOTE: passwordCredentials is intentionally omitted.
  // The Graph API rejects PUT/PATCH writes to passwordCredentials with:
  //   "New password credentials must be generated using service actions."
  // Secrets must be created via the addPassword POST action:
  //   az ad app credential reset --id <appId> --append --display-name "..."
  // The deploy workflow handles this automatically and pipes the value into
  // Key Vault.  Ref: https://github.com/microsoftgraph/msgraph-bicep-types/issues/38

  requiredResourceAccess: [
    {
      resourceAppId: apiApplication.appId
      resourceAccess: [
        {
          id: apiAccessAsUserScopeId
          type: 'Scope'    // 'Scope' = delegated; 'Role' = application
        }
      ]
    }
    {
      // Microsoft Graph standard scopes (openid, offline_access, profile)
      resourceAppId: msGraphAppId
      resourceAccess: [
        {
          id: '37f7f235-527c-4136-accd-4a02d197296e'  // openid
          type: 'Scope'
        }
        {
          id: '7427e0e9-2fba-42fe-b0c0-848c9e6a8182'  // offline_access
          type: 'Scope'
        }
        {
          id: '14dad69e-099b-42c9-810b-d002981feec1'  // profile
          type: 'Scope'
        }
      ]
    }
  ]
}

resource webServicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: webApplication.appId
  tags: [
    'woodgrove-groceries'
    environmentName
  ]
}

// ============================================================
// APP 3 — graph-middleware
// Needs application permissions to Microsoft Graph.
// Admin consent required — see README § Permissions.
// ============================================================

resource graphApplication 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'woodgrove-graph-middleware-${environmentName}'
  displayName: 'woodgrove-graph-middleware (${environmentName})'

  signInAudience: 'AzureADMyOrg'

  // Graph middleware uses client credentials (no user sign-in).
  // VERIFY: Replace these permission IDs with only the minimal set actually
  // needed — User.ReadWrite.All, Group.ReadWrite.All, Directory.ReadWrite.All
  // are broad; scope down to the least privilege for your tenant.
  requiredResourceAccess: [
    {
      resourceAppId: msGraphAppId
      resourceAccess: [
        {
          id: graphUserReadWriteAllId       // User.ReadWrite.All (application)
          type: 'Role'
        }
        {
          id: graphGroupReadWriteAllId      // Group.ReadWrite.All (application)
          type: 'Role'
        }
        {
          id: graphDirectoryReadWriteAllId  // Directory.ReadWrite.All (application)
          type: 'Role'
        }
      ]
    }
  ]

  // Certificate-based auth: keyCredentials are uploaded out-of-band.
  // See README § Certificate upload.  No passwordCredentials for graph-middleware.
}

resource graphServicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: graphApplication.appId
  tags: [
    'woodgrove-groceries'
    environmentName
  ]
}

// ============================================================
// APP 4 — auth-api (custom authentication extension host)
//
// This registration backs the Entra External ID custom authentication
// extension.  The extension itself (Microsoft.Graph/customAuthenticationExtensions)
// is NOT a supported Bicep resource type in v1.0 or beta as of 2025-08.
// It must be registered out-of-band after deployment.
// Callback URL: https://<authAppHostName>/OnTokenIssuanceStart
// ============================================================

resource authApplication 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'woodgrove-auth-api-${environmentName}'
  displayName: 'woodgrove-auth-api (${environmentName})'

  signInAudience: 'AzureADMyOrg'

  // Expose the custom authentication extension endpoint.
  // VERIFY: Entra External ID requires the application to also expose the
  // CustomAuthenticationExtension.Receive.Payload application role via
  // appRoles when registering the custom authentication extension.
  // The appRole id below is a deterministic GUID.
  appRoles: [
    {
      id: guid(environmentName, 'auth-api', 'CustomAuthenticationExtension.Receive.Payload')
      displayName: 'CustomAuthenticationExtension.Receive.Payload'
      description: 'Allows the custom authentication extension to receive payloads from Entra External ID'
      value: 'CustomAuthenticationExtension.Receive.Payload'
      allowedMemberTypes: [ 'Application' ]
      isEnabled: true
    }
  ]

  web: {
    redirectUris: [
      'https://${authAppHostName}/'
    ]
  }
}

resource authServicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: authApplication.appId
  tags: [
    'woodgrove-groceries'
    environmentName
  ]
}

// ============================================================
// OUTPUTS — consumed by main.bicep to wire app settings
// ============================================================

@description('Application (client) ID of the web / storefront app registration')
output webClientId string = webApplication.appId

@description('Application (client) ID of the API app registration')
output apiClientId string = apiApplication.appId

@description('Application (client) ID of the graph-middleware app registration')
output graphClientId string = graphApplication.appId

@description('Application (client) ID of the auth-api app registration')
output authClientId string = authApplication.appId
