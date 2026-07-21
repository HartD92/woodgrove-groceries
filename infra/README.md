# Woodgrove Groceries — Azure Infrastructure

Bicep-based IaC for the full Woodgrove Groceries environment.  
Starting from this revision, the same templates also provision the **Entra External ID (CIAM) app registrations** via the Microsoft Graph Bicep extension.

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

### Entra Resources (Microsoft Graph Bicep extension)

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

The `provisionEntraApps` parameter (default **`true`**) controls whether the Entra module runs:

| Value | Behaviour |
|---|---|
| `true` | Bicep creates/updates the four app registrations; app settings use the module outputs |
| `false` | Entra module is skipped; app settings use the `*ClientId` input params directly |

Use `false` for environments where app registrations were created before this IaC update, or where the deploy identity lacks `Application.ReadWrite.All`.

---

## Prerequisites

```bash
az --version          # ≥ 2.60.0
az bicep version      # ≥ 0.36.0  (az bicep upgrade if older)
az login              # sign in with your corporate account
az account set --subscription "<subscription-id>"
```

> **MS Graph extension** is declared in `infra/bicepconfig.json` and is **auto-restored** by the Bicep CLI from MCR before compilation — no manual install step.

---

## ⚠️ Bootstrap (One-Time Manual Step)

> **Read this before setting up CI/CD.**  
> The GitHub Actions deploy identity cannot provision itself.  The deployer app, its federated identity credential, and the `Application.ReadWrite.All` grant must be created **once** by a Global Administrator using the Azure CLI.

### 1 — Create the deployer app registration

```bash
# Adjust display name / repo slug as needed
APP_NAME="woodgrove-groceries-cicd-deployer"
REPO="HartD92/woodgrove-groceries"
TENANT_ID="<your-entra-external-id-tenant-id>"
SUBSCRIPTION_ID="<your-azure-subscription-id>"

# Create the application
APP_ID=$(az ad app create \
  --display-name "$APP_NAME" \
  --query appId -o tsv)

echo "Deployer Application (client) ID: $APP_ID"

# Create the service principal
SP_OBJECT_ID=$(az ad sp create \
  --id "$APP_ID" \
  --query id -o tsv)

echo "Deployer SP Object ID: $SP_OBJECT_ID"
```

### 2 — Add federated identity credentials (FICs)

```bash
# FIC for pushes / merge to main
az ad app federated-credential create \
  --id "$APP_ID" \
  --parameters '{
    "name": "gha-main",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:'"$REPO"':ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# FIC for the GitHub Environment "azure-infra" (workflow_dispatch + approval)
az ad app federated-credential create \
  --id "$APP_ID" \
  --parameters '{
    "name": "gha-env-azure-infra",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:'"$REPO"':environment:azure-infra",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# Optional: FIC for pull requests (read-only what-if)
az ad app federated-credential create \
  --id "$APP_ID" \
  --parameters '{
    "name": "gha-pr",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:'"$REPO"':pull_request",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

### 3 — Grant Azure RBAC

```bash
# Contributor on the subscription (needed to create resource groups + resources)
az role assignment create \
  --assignee-object-id "$SP_OBJECT_ID" \
  --assignee-principal-type ServicePrincipal \
  --role Contributor \
  --scope "/subscriptions/$SUBSCRIPTION_ID"

# OPTIONAL but required for Key Vault RBAC assignments:
az role assignment create \
  --assignee-object-id "$SP_OBJECT_ID" \
  --assignee-principal-type ServicePrincipal \
  --role "User Access Administrator" \
  --scope "/subscriptions/$SUBSCRIPTION_ID"
```

### 4 — Grant MS Graph permissions + admin consent

```bash
# MS Graph service principal object ID (same in every tenant)
MSGRAPH_SP=$(az ad sp show --id "00000003-0000-0000-c000-000000000000" --query id -o tsv)

# Application.ReadWrite.All  (app role ID: 1bfefb4e-e0b5-418b-a88f-73c46d2cc8e9)
az ad app permission add \
  --id "$APP_ID" \
  --api "00000003-0000-0000-c000-000000000000" \
  --api-permissions "1bfefb4e-e0b5-418b-a88f-73c46d2cc8e9=Role"

# AppRoleAssignment.ReadWrite.All  (needed to grant consent to other apps)
# App role ID: 06b708a9-e830-4db3-a914-8e69da51d44f
az ad app permission add \
  --id "$APP_ID" \
  --api "00000003-0000-0000-c000-000000000000" \
  --api-permissions "06b708a9-e830-4db3-a914-8e69da51d44f=Role"

# Admin consent (requires Global Administrator or Privileged Role Administrator)
az ad app permission admin-consent --id "$APP_ID"
```

> **VERIFY:** The app role IDs above are the well-known stable GUIDs documented at  
> https://learn.microsoft.com/en-us/graph/permissions-reference  
> Confirm against current docs before running.

### 5 — Set GitHub Actions variables

In the GitHub repository → **Settings → Secrets and variables → Actions → Variables**:

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID` | `$APP_ID` from step 1 |
| `AZURE_TENANT_ID` | Your Entra External ID tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Your Azure subscription ID |

> These are **variables** (not secrets) because they're non-sensitive identifiers.  
> The OIDC flow never requires a client secret stored in GitHub.

### 6 — Create GitHub Environment

In GitHub repository → **Settings → Environments → New environment**:
- Name: `azure-infra`
- Add required reviewers for production protection (optional for dev)

---

## Deploy

### Option A — GitHub Actions (recommended)

1. Complete all bootstrap steps above.
2. Push to `main` (auto-triggers on `infra/**` changes) or use  
   **Actions → Deploy Infrastructure → Run workflow** for manual control.

### Option B — Local CLI

```bash
az login
az account set --subscription "<subscription-id>"

# Validate (lint + build)
az bicep lint --file infra/main.bicep
az bicep build --file infra/main.bicep

# What-if (plan)
az deployment sub what-if \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam

# Deploy
az deployment sub create \
  --name woodgrove-deploy-dev \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam
```

---

## Post-Deploy: Seed Key Vault Secrets

> ⚠️ App settings using `@Microsoft.KeyVault(...)` references will show as "secret reference pending" until the corresponding KV secrets exist.

```bash
KV_NAME=$(az deployment sub show --name woodgrove-deploy-dev \
  --query "properties.outputs.keyVaultName.value" -o tsv)

# 1 — Application Insights connection string
AI_CONN=$(az monitor app-insights component show \
  --app appi-woodgrove-dev \
  --resource-group rg-woodgrove-dev \
  --query connectionString -o tsv)
az keyvault secret set --vault-name $KV_NAME \
  --name appinsights-connection-string --value "$AI_CONN"

# 2 — ACS connection string (primary key)
ACS_CONN=$(az communication list-key \
  --name acs-woodgrove-dev-<suffix> \
  --resource-group rg-woodgrove-dev \
  --query primaryConnectionString -o tsv)
az keyvault secret set --vault-name $KV_NAME \
  --name acs-connection-string --value "$ACS_CONN"

# 3 — Web app client secret (idempotent — automated by GitHub Actions when provisionEntraApps=true)
#
# The deploy workflow is now idempotent for this secret:
#   • If web-client-secret already exists in Key Vault → SKIP (no credential is minted or rotated)
#   • If absent (first provision) + provisionEntraApps=true → CREATE a 1-year credential via addPassword
#   • rotate_web_secret=true dispatch input → FORCE RESET + KV update + prune expired app-reg creds
#
# For local/manual first-provision deploys (run only if the secret is absent in Key Vault):
WEB_CLIENT_ID=$(az deployment sub show --name woodgrove-deploy-dev \
  --query "properties.outputs.resolvedWebClientId.value" -o tsv)
SECRET_JSON=$(az ad app credential reset \
  --id "$WEB_CLIENT_ID" \
  --append \
  --display-name "cicd-managed-dev" \
  --years 1 \
  --output json)
az keyvault secret set --vault-name $KV_NAME \
  --name web-client-secret \
  --value "$(echo $SECRET_JSON | python3 -c 'import json,sys; print(json.load(sys.stdin)[\"password\"])')"
# NOTE: --append preserves existing credentials; the secret value is only
# returned once at creation time.  endDateTime (1-year expiry) is set by
# the CLI, not by Bicep (passwordCredentials is a Bicep write restriction).
# To rotate via CI: Actions → Deploy Infrastructure → Run workflow → check rotate_web_secret.

# 4 — Cloudflare API token / secret
az keyvault secret set --vault-name $KV_NAME \
  --name cloudflare-api-secret --value "<paste-cloudflare-token>"
```

> **Rotating `web-client-secret`:** Trigger **Actions → Deploy Infrastructure → Run workflow**, set `rotate_web_secret = true`. The workflow will reset the credential with `--append` (so a valid secret is always live), write the new value to Key Vault, and then prune any **expired** credentials from the app registration to prevent orphaned-but-valid creds from accumulating. Never run `az ad app credential reset` unconditionally on every `infra/**` push — doing so mints new credentials each time and leaves the old ones alive until their 1-year expiry.

---

`Microsoft.Graph/customAuthenticationExtensions` is **not available as a Bicep resource type** (v1.0 or beta, as of August 2025). Register it with `az rest` after deployment:

```bash
AUTH_HOST=$(az deployment sub show --name woodgrove-deploy-dev \
  --query "properties.outputs.authAppHostName.value" -o tsv)

AUTH_CLIENT_ID=$(az deployment sub show --name woodgrove-deploy-dev \
  --query "properties.outputs.resolvedAuthClientId.value" -o tsv)

TENANT_ID="<your-entra-external-id-tenant-id>"

# Create the custom authentication extension
az rest \
  --method POST \
  --uri "https://graph.microsoft.com/v1.0/identity/customAuthenticationExtensions" \
  --headers "Content-Type=application/json" \
  --body '{
    "@odata.type": "#microsoft.graph.onTokenIssuanceStartCustomExtension",
    "displayName": "woodgrove-auth-api",
    "description": "Woodgrove custom authentication extension",
    "endpointConfiguration": {
      "@odata.type": "#microsoft.graph.httpRequestEndpoint",
      "targetUrl": "https://'"$AUTH_HOST"'/api/CustomAuthenticationExtension"
    },
    "authenticationConfiguration": {
      "@odata.type": "#microsoft.graph.azureAdTokenAuthentication",
      "resourceId": "'"$AUTH_CLIENT_ID"'"
    }
  }'
```

After creating the extension, wire it to your Entra External ID user flow in the portal:  
**Entra External ID → User flows → [your flow] → Custom authentication extensions**.

---

## Post-Deploy: Configure Custom Domains

```bash
APP_NAME="app-woodgrove-web-dev-<suffix>"
RG="rg-woodgrove-dev"

az webapp config hostname add \
  --webapp-name $APP_NAME --resource-group $RG \
  --hostname woodgrovedemo.com

az webapp config ssl bind \
  --name $APP_NAME --resource-group $RG \
  --certificate-thumbprint "<thumbprint>" --ssl-type SNI
```

---

## Post-Deploy: Certificate Upload (graph-middleware)

The graph-middleware app uses certificate-based client authentication against Microsoft Graph.  
Upload the PFX to Key Vault (cert sync to App Service is automatic via the Key Vault Certificate User role):

```bash
az keyvault certificate import \
  --vault-name $KV_NAME \
  --name graph-middleware-cert \
  --file /path/to/cert.pfx \
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
| **Azure Contributor** on subscription | Creating all Azure resources |
| **User Access Administrator** on subscription | Creating Key Vault RBAC role assignments |

> David: If the deployer identity only has `Application.ReadWrite.All`, the app registrations will be created/updated correctly but admin consent for graph-middleware Graph permissions must be granted manually in the Entra portal.

---

## Module Reference

| File | Scope | Description |
|---|---|---|
| `main.bicep` | `subscription` | Orchestrator: creates RG, wires modules |
| `modules/entraApps.bicep` | Entra tenant (Graph) | 4 app registrations + service principals |
| `modules/appServicePlan.bicep` | resource group | Windows App Service Plan |
| `modules/webApp.bicep` | resource group | Reusable Windows web app (system identity, always-on, HTTPS, cert loading) |
| `modules/keyVault.bicep` | resource group | RBAC-enabled Key Vault; role assignments for each app's managed identity |
| `modules/monitoring.bicep` | resource group | Log Analytics Workspace + workspace-based Application Insights |
| `modules/communicationServices.bicep` | resource group | ACS + Email Service + Azure-managed domain |

---

## Environment Teardown

```bash
az group delete --name rg-woodgrove-dev --yes --no-wait
az keyvault purge --name kv-wg-dev-<suffix> --location eastus2

# Entra app registrations are NOT deleted by the resource group deletion.
# Delete them separately if needed:
az ad app delete --id <web-app-client-id>
az ad app delete --id <api-app-client-id>
az ad app delete --id <graph-middleware-client-id>
az ad app delete --id <auth-api-client-id>
```
