# Woodgrove Groceries — Azure Infrastructure

Bicep-based IaC for the full Woodgrove Groceries environment.  
Azure resources deploy from the workforce subscription tenant. Entra External ID (CIAM) app registrations are provisioned separately in the ExtID tenant by the GitHub Actions workflow.

---

## Architecture Overview

```
infra/
  bicepconfig.json         ← MS Graph extension declaration
  main.bicep               ← Subscription-scoped orchestrator
  main.bicepparam          ← Parameter values (no secrets)
  modules/
    entraApps.bicep        ← NEW: Entra app registrations + service principals
    appServicePlan.bicep
    webApp.bicep
    keyVault.bicep
    monitoring.bicep
    communicationServices.bicep
.github/workflows/
  deploy-infra.yml         ← NEW: OIDC-based GitHub Actions deploy workflow
```

### Azure Resources

| Resource | Naming pattern | Notes |
|---|---|---|
| Resource Group | `rg-woodgrove-<env>` | Created by `main.bicep` (subscription scope) |
| App Service Plan | `plan-woodgrove-main-<env>` | Windows, P1v3 by default |
| Web app — storefront | `app-woodgrove-web-<env>-<suffix>` | `woodgrovedemo.com` |
| Web app — API | `app-woodgrove-api-<env>-<suffix>` | `api.woodgrovedemo.com` |
| Web app — graph-middleware | `app-woodgrove-graph-<env>-<suffix>` | `graph-middleware.woodgrovedemo.com` |
| Web app — auth-api | `app-woodgrove-auth-<env>-<suffix>` | **Stable HTTPS endpoint required by Entra** |
| Key Vault | `kv-wg-<env>-<suffix>` | RBAC-enabled; all four apps granted Secrets User + Certificate User |
| Log Analytics Workspace | `log-woodgrove-<env>` | |
| Application Insights | `appi-woodgrove-<env>` | Workspace-based |
| Azure Communication Services | `acs-woodgrove-<env>-<suffix>` | Linked to Azure-managed email domain |
| ACS Email Service | `email-woodgrove-<env>-<suffix>` | Azure-managed `@azurecomm.net` domain (dev/test) |

### Entra Resources (External ID tenant, provisioned by CI/CD CLI)

| Resource | `uniqueName` | Notes |
|---|---|---|
| Application + SP — storefront | `woodgrove-groceries-web-<env>` | Redirect URIs, ID tokens enabled, client secret |
| Application + SP — API | `woodgrove-groceries-api-<env>` | Exposes `access_as_user` delegated scope |
| Application + SP — graph-middleware | `woodgrove-graph-middleware-<env>` | MS Graph application permissions |
| Application + SP — auth-api | `woodgrove-auth-api-<env>` | Hosts custom authentication extension |

All four web apps use **system-assigned managed identity**; `WEBSITE_LOAD_CERTIFICATES=*` ensures client certs are available in the Windows cert store.  
Secrets are Key Vault references (`@Microsoft.KeyVault(SecretUri=...)`); non-secret config comes from `main.bicepparam`.

---

## `provisionEntraApps` Toggle

The `provisionEntraApps` parameter (default **`false`**) controls whether the deprecated Graph Bicep module runs:

| Value | Behaviour |
|---|---|
| `false` | Supported path. Entra module is skipped; app settings use the `*ClientId` input params supplied by the ExtID provisioning job. |
| `true` | Backward-compatibility only. Bicep creates/updates the four app registrations in the deployment principal's tenant. Do **not** use this when the Azure subscription tenant and ExtID tenant are different. |

For Woodgrove, keep this **false**. Subscription-scoped deployments authenticate to the workforce tenant, and the Microsoft Graph Bicep extension cannot target the separate ExtID tenant.

---

## Prerequisites

```powershell
az --version          # ≥ 2.60.0
az bicep version      # ≥ 0.36.0  (az bicep upgrade if older)
az login              # sign in with your corporate account
az account set --subscription "<subscription-id>"
```

> **MS Graph extension** is declared in `infra/bicepconfig.json` and is **auto-restored** by the Bicep CLI from MCR before compilation — no manual install step.

---

## ⚠️ Bootstrap (One-Time Manual Step)

> **Read this before setting up CI/CD.**  Woodgrove uses two Entra tenants:
>
> - **Workforce tenant:** owns the Azure subscription. Identity A deploys ARM/Bicep resources here.
> - **External ID / CIAM tenant:** owns the customer-facing app registrations. Identity B provisions those Entra objects here.
>
> The workforce↔ExtID billing link is **not** an authentication trust. A GitHub OIDC federated identity credential (FIC) is only valid in the tenant where that app registration lives.

> [!WARNING]
> **Determine your OIDC subject format FIRST.** Some GitHub organizations and enterprises enforce a customized OIDC subject claim that prepends immutable numeric IDs (owner ID + repo ID) to the subject. Microsoft-managed organizations commonly do this. Before creating any FIC, discover your effective subject prefix:
>
> ```powershell
> gh api /repos/<owner>/<repo>/actions/oidc/customization/sub
> ```
>
> Read the `sub_claim_prefix` field. If it looks like `repo:<owner>@<id>/<repo>@<id>`, use that exact prefix in every FIC subject. If it is absent, or the response uses the default plain `repo:<owner>/<repo>` prefix, use the standard slug form.
>
> For this repository, capture the prefix once and reuse it:
>
> ```powershell
> $SubPrefix = (gh api /repos/HartD92/woodgrove-groceries/actions/oidc/customization/sub | ConvertFrom-Json).sub_claim_prefix
> if (-not $SubPrefix) { $SubPrefix = "repo:HartD92/woodgrove-groceries" }
> ```
>
> Build FIC subjects as `"$($SubPrefix):environment:azure-infra"` and `"$($SubPrefix):ref:refs/heads/main"`. Use the `$($SubPrefix)` subexpression form so PowerShell does not parse `:` as a drive qualifier.

### Tenant / identity map

| Purpose | Tenant | GitHub variables | Permissions |
|---|---|---|---|
| Identity A — Azure resource deployment | Workforce tenant that owns the subscription | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | Azure RBAC: Contributor + User Access Administrator on the subscription |
| Identity B — CIAM app registration provisioning | Entra External ID tenant | `ENTRA_CLIENT_ID`, `ENTRA_TENANT_ID` | Microsoft Graph app roles: `Application.ReadWrite.All`, `AppRoleAssignment.ReadWrite.All`, `DelegatedPermissionGrant.ReadWrite.All`, `Policy.ReadWrite.AuthenticationMethod` (required to enable Email OTP for SSPR — see § Self-service password reset) |

### Registering Identity A in the workforce tenant

Use the existing workforce deploy identity if it already exists. If creating it from scratch, sign in to the **workforce tenant** and assign Azure RBAC on the subscription:

```powershell
$AppName = "woodgrove-groceries-arm-deployer"
$SubPrefix = (gh api /repos/HartD92/woodgrove-groceries/actions/oidc/customization/sub | ConvertFrom-Json).sub_claim_prefix
if (-not $SubPrefix) { $SubPrefix = "repo:HartD92/woodgrove-groceries" }
$WorkforceTenantId = "<your-workforce-tenant-id>"
$SubscriptionId = "<your-azure-subscription-id>"

az login --tenant $WorkforceTenantId
az account set --subscription $SubscriptionId

$AzureAppId = az ad app create `
  --display-name $AppName `
  --query appId -o tsv

$AzureSpObjectId = az ad sp create `
  --id $AzureAppId `
  --query id -o tsv

$fic = [ordered]@{
  name = 'gha-env-azure-infra'
  issuer = 'https://token.actions.githubusercontent.com'
  subject = "$($SubPrefix):environment:azure-infra"
  audiences = @('api://AzureADTokenExchange')
}
$fic | ConvertTo-Json -Depth 5 | Out-File -FilePath fic-env.json -Encoding utf8
az ad app federated-credential create --id $AzureAppId --parameters '@fic-env.json'
Remove-Item fic-env.json

# Optional FIC for non-environment triggers; deploy-infra.yml uses the azure-infra environment.
$fic = [ordered]@{
  name = 'gha-main'
  issuer = 'https://token.actions.githubusercontent.com'
  subject = "$($SubPrefix):ref:refs/heads/main"
  audiences = @('api://AzureADTokenExchange')
}
$fic | ConvertTo-Json -Depth 5 | Out-File -FilePath fic-main.json -Encoding utf8
az ad app federated-credential create --id $AzureAppId --parameters '@fic-main.json'
Remove-Item fic-main.json

az role assignment create `
  --assignee-object-id $AzureSpObjectId `
  --assignee-principal-type ServicePrincipal `
  --role Contributor `
  --scope "/subscriptions/$SubscriptionId"

az role assignment create `
  --assignee-object-id $AzureSpObjectId `
  --assignee-principal-type ServicePrincipal `
  --role "User Access Administrator" `
  --scope "/subscriptions/$SubscriptionId"
```

Set GitHub Actions variables:

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID` | `$AzureAppId` |
| `AZURE_TENANT_ID` | `$WorkforceTenantId` |
| `AZURE_SUBSCRIPTION_ID` | `$SubscriptionId` |

### Registering Identity B in the ExtID tenant

Sign in to the **External ID / CIAM tenant**. This identity does not need Azure subscription RBAC; it only needs Microsoft Graph application permissions in the ExtID tenant.

```powershell
$AppName = "woodgrove-groceries-extid-entra-provisioner"
$SubPrefix = (gh api /repos/HartD92/woodgrove-groceries/actions/oidc/customization/sub | ConvertFrom-Json).sub_claim_prefix
if (-not $SubPrefix) { $SubPrefix = "repo:HartD92/woodgrove-groceries" }
$ExtIdTenantId = "<your-entra-external-id-tenant-id>"

az login --tenant $ExtIdTenantId --allow-no-subscriptions

$EntrAppId = az ad app create `
  --display-name $AppName `
  --query appId -o tsv

$EntrSpObjectId = az ad sp create `
  --id $EntrAppId `
  --query id -o tsv

# FIC for the GitHub Environment "azure-infra" (used by deploy-infra.yml)
$fic = [ordered]@{
  name = 'gha-env-azure-infra'
  issuer = 'https://token.actions.githubusercontent.com'
  subject = "$($SubPrefix):environment:azure-infra"
  audiences = @('api://AzureADTokenExchange')
}
$fic | ConvertTo-Json -Depth 5 | Out-File -FilePath fic-env.json -Encoding utf8
az ad app federated-credential create --id $EntrAppId --parameters '@fic-env.json'
Remove-Item fic-env.json

# Optional FIC for non-environment triggers.
$fic = [ordered]@{
  name = 'gha-main'
  issuer = 'https://token.actions.githubusercontent.com'
  subject = "$($SubPrefix):ref:refs/heads/main"
  audiences = @('api://AzureADTokenExchange')
}
$fic | ConvertTo-Json -Depth 5 | Out-File -FilePath fic-main.json -Encoding utf8
az ad app federated-credential create --id $EntrAppId --parameters '@fic-main.json'
Remove-Item fic-main.json
```

Grant Microsoft Graph permissions and admin consent in the **ExtID tenant**:

```powershell
$MicrosoftGraphAppId = "00000003-0000-0000-c000-000000000000"

# VERIFY: Confirm these Graph app role IDs against
# https://learn.microsoft.com/en-us/graph/permissions-reference before running.
$ApplicationReadWriteAll = "1bfefb4e-e0b5-418b-a88f-73c46d2cc8e9"          # Application.ReadWrite.All
$AppRoleAssignmentReadWriteAll = "06b708a9-e830-4db3-a914-8e69da51d44f"    # AppRoleAssignment.ReadWrite.All
$DelegatedPermissionGrantReadWriteAll = "8e8e4742-1d95-4f68-9d56-6ee75648c72a" # VERIFY: DelegatedPermissionGrant.ReadWrite.All

az ad app permission add `
  --id $EntrAppId `
  --api $MicrosoftGraphAppId `
  --api-permissions "$($ApplicationReadWriteAll)=Role"

az ad app permission add `
  --id $EntrAppId `
  --api $MicrosoftGraphAppId `
  --api-permissions "$($AppRoleAssignmentReadWriteAll)=Role"

az ad app permission add `
  --id $EntrAppId `
  --api $MicrosoftGraphAppId `
  --api-permissions "$($DelegatedPermissionGrantReadWriteAll)=Role"

az ad app permission admin-consent --id $EntrAppId
```

Set GitHub Actions variables:

| Variable | Value |
|---|---|
| `ENTRA_CLIENT_ID` | `$EntrAppId` |
| `ENTRA_TENANT_ID` | `$ExtIdTenantId` |

### GitHub Environment

In GitHub repository → **Settings → Environments → New environment**:
- Name: `azure-infra`
- Add required reviewers for production protection (optional for dev)

### Troubleshooting OIDC FIC matching

If a workflow run fails at `azure/login` with `AADSTS700213: No matching federated identity record found`, copy the exact `presented assertion subject` value from the run log. In the tenant being logged into, ensure the app registration has a FIC with that **exact** subject string. The environment subject is the one actually used by both `deploy-infra.yml` jobs because they specify `environment: azure-infra`; the main-branch ref subject is only for non-environment triggers.

---

## Deploy

### Option A — GitHub Actions (recommended)

1. Complete all bootstrap steps above.
2. Push to `main` (auto-triggers on `infra/**` changes) or use  
   **Actions → Deploy Infrastructure → Run workflow** for manual control.

### Option B — Local CLI

```powershell
az login
az account set --subscription "<subscription-id>"

# Validate (lint + build)
az bicep lint --file infra/main.bicep
az bicep build --file infra/main.bicep

# Fill webClientId/apiClientId/graphClientId/authClientId in infra/main.bicepparam,
# or override them on the command line with the values from the ExtID provisioning job.

# What-if (plan)
az deployment sub what-if `
  --location eastus2 `
  --template-file infra/main.bicep `
  --parameters infra/main.bicepparam

# Deploy
az deployment sub create `
  --name woodgrove-deploy-dev `
  --location eastus2 `
  --template-file infra/main.bicep `
  --parameters infra/main.bicepparam
```

---

## Post-Deploy: Seed Key Vault Secrets

> ⚠️ App settings using `@Microsoft.KeyVault(...)` references will show as "secret reference pending" until the corresponding KV secrets exist.

```powershell
$KvName = az deployment sub show --name woodgrove-deploy-dev `
  --query "properties.outputs.keyVaultName.value" -o tsv

# 1 — Application Insights connection string
$AiConn = az monitor app-insights component show `
  --app appi-woodgrove-dev `
  --resource-group rg-woodgrove-dev `
  --query connectionString -o tsv
az keyvault secret set --vault-name $KvName `
  --name appinsights-connection-string --value $AiConn

# 2 — ACS connection string (primary key)
$AcsConn = az communication list-key `
  --name acs-woodgrove-dev-<suffix> `
  --resource-group rg-woodgrove-dev `
  --query primaryConnectionString -o tsv
az keyvault secret set --vault-name $KvName `
  --name acs-connection-string --value $AcsConn

# 3 — Web app client secret (idempotent — automated by GitHub Actions)
#
# The deploy workflow is now idempotent for this secret:
#   • If web-client-secret already exists in Key Vault → SKIP (no credential is minted or rotated)
#   • If absent (first provision) → CREATE a 1-year credential in ExtID via addPassword
#   • rotate_web_secret=true dispatch input → FORCE RESET + KV update + prune expired app-reg creds
#
# For local/manual first-provision deploys, run only if the secret is absent in Key Vault.
# First, while signed into the workforce tenant, read the client ID from deployment outputs:
$WebClientId = az deployment sub show --name woodgrove-deploy-dev `
  --query "properties.outputs.resolvedWebClientId.value" -o tsv

# Then sign into the ExtID tenant to create the CIAM app credential:
az login --tenant "<your-entra-external-id-tenant-id>" --allow-no-subscriptions
$SecretJson = az ad app credential reset `
  --id $WebClientId `
  --append `
  --display-name "cicd-managed-dev" `
  --years 1 `
  -o json | ConvertFrom-Json

# Finally, sign back into the workforce tenant to store the secret in Key Vault:
az login --tenant "<your-workforce-tenant-id>"
az account set --subscription "<your-azure-subscription-id>"
az keyvault secret set --vault-name $KvName `
  --name web-client-secret --value $SecretJson.password
# NOTE: --append preserves existing credentials; the secret value is only
# returned once at creation time.  endDateTime (1-year expiry) is set by
# the CLI, not by Bicep (passwordCredentials is a Bicep write restriction).
# To rotate via CI: Actions → Deploy Infrastructure → Run workflow → check rotate_web_secret.

# 4 — Cloudflare API token / secret
az keyvault secret set --vault-name $KvName `
  --name cloudflare-api-secret --value "<paste-cloudflare-token>"
```

> **Rotating `web-client-secret`:** Trigger **Actions → Deploy Infrastructure → Run workflow**, set `rotate_web_secret = true`. The workflow will reset the credential with `--append` (so a valid secret is always live), write the new value to Key Vault, and then prune any **expired** credentials from the app registration to prevent orphaned-but-valid creds from accumulating. Never run `az ad app credential reset` unconditionally on every `infra/**` push — doing so mints new credentials each time and leaves the old ones alive until their 1-year expiry.

---

`Microsoft.Graph/customAuthenticationExtensions` is **not available as a Bicep resource type** (v1.0 or beta, as of August 2025). Register it with `az rest` after deployment:

```powershell
$AuthHost = az deployment sub show --name woodgrove-deploy-dev `
  --query "properties.outputs.authAppHostName.value" -o tsv

$AuthClientId = az deployment sub show --name woodgrove-deploy-dev `
  --query "properties.outputs.resolvedAuthClientId.value" -o tsv

$TenantId = "<your-entra-external-id-tenant-id>"

# Create the custom authentication extension
# Build the body as a hashtable to avoid inline JSON quote-mangling on Windows
$authExtBody = [ordered]@{
  '@odata.type' = '#microsoft.graph.onTokenIssuanceStartCustomExtension'
  displayName   = 'woodgrove-auth-api'
  description   = 'Woodgrove custom authentication extension'
  endpointConfiguration = [ordered]@{
    '@odata.type' = '#microsoft.graph.httpRequestEndpoint'
    targetUrl     = "https://$($AuthHost)/OnTokenIssuanceStart"
  }
  authenticationConfiguration = [ordered]@{
    '@odata.type' = '#microsoft.graph.azureAdTokenAuthentication'
    resourceId    = $AuthClientId
  }
}
$authExtBody | ConvertTo-Json -Depth 5 | Out-File -FilePath auth-ext.json -Encoding utf8
az rest `
  --method POST `
  --uri "https://graph.microsoft.com/v1.0/identity/customAuthenticationExtensions" `
  --headers "Content-Type=application/json" `
  --body '@auth-ext.json'
Remove-Item auth-ext.json
```

After creating the extension, wire it to your Entra External ID user flow in the portal:  
**Entra External ID → User flows → [your flow] → Custom authentication extensions**.

---

## Post-Deploy: Configure Custom Domains

```powershell
$AppName = "app-woodgrove-web-dev-<suffix>"
$Rg = "rg-woodgrove-dev"

az webapp config hostname add `
  --webapp-name $AppName --resource-group $Rg `
  --hostname woodgrovedemo.com

az webapp config ssl bind `
  --name $AppName --resource-group $Rg `
  --certificate-thumbprint "<thumbprint>" --ssl-type SNI
```

---

## Post-Deploy: Certificate Upload (graph-middleware)

The graph-middleware app uses certificate-based client authentication against Microsoft Graph.  
Upload the PFX to Key Vault (cert sync to App Service is automatic via the Key Vault Certificate User role):

```powershell
az keyvault certificate import `
  --vault-name $KvName `
  --name graph-middleware-cert `
  --file /path/to/cert.pfx `
  --password "<pfx-password>"
```

After the cert syncs to App Service, **manually add its thumbprint to the graph-middleware app registration** in the Entra portal:  
**App registrations → woodgrove-graph-middleware → Certificates & secrets → Certificates → Upload certificate**.

---

## Permissions Reference

### What `Application.ReadWrite.All` covers

| Operation | Covered? |
|---|---|
| Create / update app registrations | ✅ |
| Create / update service principals | ✅ |
| Add federated identity credentials | ✅ |
| Set app roles / scopes on registrations | ✅ |
| **Grant admin consent** for app roles | ❌ — needs `AppRoleAssignment.ReadWrite.All` |
| **Create delegated permission grants** | ❌ — needs `DelegatedPermissionGrant.ReadWrite.All` |
| **Upload certificates** to registrations | ❌ — out-of-band via portal or Graph API call |

### Minimum permissions for full automation

| Permission | Needed for |
|---|---|
| `Application.ReadWrite.All` (application) | Creating/updating all 4 app registrations + SPs |
| `AppRoleAssignment.ReadWrite.All` (application) | Granting admin consent for graph-middleware MS Graph app roles |
| `DelegatedPermissionGrant.ReadWrite.All` (application) | Creating delegated OAuth2 grants for web → API scopes |
| `OrganizationalBranding.ReadWrite.All` (application) | Applying External ID company branding assets, colors, text, and custom CSS via Microsoft Graph |
| `Policy.ReadWrite.AuthenticationMethod` (application) | Enabling the Email OTP and SMS authentication methods for self-service password reset (SSPR) and SMS sign-in |
| **Azure Contributor** on subscription | Creating all Azure resources |
| **User Access Administrator** on subscription | Creating Key Vault RBAC role assignments |

> David: If the deployer identity only has `Application.ReadWrite.All`, the app registrations will be created/updated correctly but admin consent for graph-middleware Graph permissions must be granted manually in the Entra portal.

---

## Post-Deploy: Company Branding

This repo is public, so it keeps only the A&F theme code/text in-repo
(`src/storefront/wwwroot/Company-branding/af-custom.css` and `login-text-*.md`).
Host the actual image assets in the private brand-assets container and expose
them via:

- `BrandAssets__BaseUrl`
- `BRAND_ASSETS_BASE_URL`

Automation helper:

- `infra/scripts/Apply-CompanyBranding.ps1`

The script:

- `PATCH`es the default `organizationalBranding` JSON properties
- uploads `bannerLogo`, `headerLogo`, `backgroundImage`, `favicon`, `customCSS`, `squareLogo`, and `squareLogoDark`
- reuses the same sign-in text used by `src/auth-api/Controllers/onPageRenderStartController.cs`
- can pull the required image files from `BrandAssets__BaseUrl` instead of from committed repo assets

Run it after signing in to the target External ID tenant:

```powershell
az login --tenant <extid-tenant-id> --allow-no-subscriptions
.\infra\scripts\Apply-CompanyBranding.ps1 `
  -OrganizationId <extid-tenant-id> `
  -TenantId <extid-tenant-id> `
  -BrandAssetsBaseUrl https://<private-brand-host>/brand-assets
```

This requires `OrganizationalBranding.ReadWrite.All`.

Expected files at that base URL:

- `af-logo.svg`
- `af-logo-light.svg`
- `af-background.jpg`
- `af-favicon.png`
- `af-headerlogo.png`
- `af-bannerlogo.png`
- `af-square-logo-light.png`
- `af-square-logo-dark.png`

Two branding-adjacent steps still remain manual/live-tenant specific:

1. Complete and validate the custom URL domain rollout (`login.woodgrovegroceries.com`) in Entra External ID + Azure Front Door.
2. Enable **Show self-service password reset** in Company Branding → Sign-in form if the demo flow should expose that link.

---

## Post-Deploy: Self-Service Password Reset (SSPR)

The `entra-provision` job automatically `PATCH`es
`https://graph.microsoft.com/v1.0/policies/authenticationMethodsPolicy/authenticationMethodConfigurations/email`
to enable the **Email OTP** authentication method tenant-wide
(`allowExternalIdToUseEmailOtp: default`, targeting `all_users`). This is the
authentication-method prerequisite for SSPR described in
`src/storefront/Areas/Help/Pages/SSPR.cshtml`.

This automates step 2–4 of that help page. Two steps remain **manual** because
they are not exposed via Microsoft Graph as of this writing:

1. Confirm the sign-up/sign-in user flow registers **Email with password** as an
   identity provider (Entra admin center → External Identities → User flows →
   your flow → Identity providers). This repo's app/user-flow provisioning does
   not manage user-flow identity-provider selection.
2. Enable **"Show self-service password reset"** in Company Branding → Sign-in
   form (Entra admin center → Identity → User experience → Company Branding).
   The `organizationalBranding` Graph API does not currently expose this
   toggle, so it must be enabled by hand.

If the deployer identity lacks `Policy.ReadWrite.AuthenticationMethod`, the
Email OTP PATCH step logs a warning and continues (non-fatal) — enable Email
OTP manually per `SSPR.cshtml` steps 3–4 in that case.

---

## Post-Deploy: SMS Authentication

The `entra-provision` job also creates/reuses a dedicated security group
(**"Woodgrove SMS Authentication"**, `mailNickname: woodgroveSmsAuthentication`)
and `PATCH`es
`https://graph.microsoft.com/v1.0/policies/authenticationMethodsPolicy/authenticationMethodConfigurations/sms`
to enable the **SMS** authentication method for that group.

Unlike Email OTP, SMS's `includeTargets` does **not** support a synthetic
"all users" target — the Graph API only accepts a real Entra group object ID
(`targetType: 'group'`). This means SMS is opt-in: no users are enrolled by
default. To let a user sign in / verify with SMS, add them to the
**"Woodgrove SMS Authentication"** group in the Entra admin center (Groups →
find the group → Members → Add members).

Two manual prerequisites are **not** automatable via Graph and must be
completed once, by hand, before the SMS PATCH will succeed:

1. **Link the CIAM tenant to an Azure subscription with billing enabled.**
   Unlike Email OTP (included in base MAU billing), SMS/voice authentication
   is a **pay-as-you-go premium add-on** billed per-transaction. Entra admin
   center → tenant overview → link/verify the associated Azure subscription.
2. The deployer identity needs `Policy.ReadWrite.AuthenticationMethod` (same
   permission Email OTP uses — see § Minimum permissions above).

If either prerequisite is missing, the SMS PATCH step logs a warning and
continues (non-fatal) rather than failing the deployment — enable SMS
manually afterward per
`src/storefront/Areas/Help/Pages/SmsAuthentication.cshtml`.

> Note: as of this writing, Microsoft's Feb 2027 retirement of
> Microsoft-provided SMS/voice authentication applies to **workforce**
> tenants only — Entra External ID (CIAM) and Azure AD B2C tenants are
> explicitly excluded from that timeline (see
> [the FAQ](https://learn.microsoft.com/en-us/entra/identity/authentication/concept-sms-voice-retirement-faq)).
> No retirement date has been announced for CIAM/B2C at this time.

---

## Module Reference

| File | Scope | Description |
|---|---|---|
| `main.bicep` | `subscription` | Orchestrator: creates RG, wires modules |
| `modules/entraApps.bicep` | Entra tenant (Graph) | Deprecated deployment path; retained as the CIAM app-registration spec mirrored by the workflow CLI provisioning job |
| `modules/appServicePlan.bicep` | resource group | Windows App Service Plan |
| `modules/webApp.bicep` | resource group | Reusable Windows web app (system identity, always-on, HTTPS, cert loading) |
| `modules/keyVault.bicep` | resource group | RBAC-enabled Key Vault; role assignments for each app's managed identity |
| `modules/monitoring.bicep` | resource group | Log Analytics Workspace + workspace-based Application Insights |
| `modules/communicationServices.bicep` | resource group | ACS + Email Service + Azure-managed domain |

---

## Environment Teardown

```powershell
az group delete --name rg-woodgrove-dev --yes --no-wait
az keyvault purge --name kv-wg-dev-<suffix> --location eastus2

# Entra app registrations are NOT deleted by the resource group deletion.
# Delete them separately if needed:
az ad app delete --id <web-app-client-id>
az ad app delete --id <api-app-client-id>
az ad app delete --id <graph-middleware-client-id>
az ad app delete --id <auth-api-client-id>
```
