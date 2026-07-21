# Woodgrove Groceries — Azure Infrastructure

Bicep-based IaC for the full Woodgrove Groceries environment.

## What Gets Deployed

| Resource | Naming pattern | Notes |
|---|---|---|
| Resource Group | `rg-woodgrove-<env>` | Created by `main.bicep` (subscription scope) |
| App Service Plan | `plan-woodgrove-main-<env>` | Windows, P1v3 by default |
| Web app — woodgrove-groceries | `app-woodgrove-web-<env>-<suffix>` | `woodgrovedemo.com` |
| Web app — woodgrove-groceries-api | `app-woodgrove-api-<env>-<suffix>` | `api.woodgrovedemo.com` |
| Web app — graph-middleware | `app-woodgrove-graph-<env>-<suffix>` | `graph-middleware.woodgrovedemo.com` |
| Web app — auth-api | `app-woodgrove-auth-<env>-<suffix>` | **Stable HTTPS endpoint required by Entra** |
| Key Vault | `kv-wg-<env>-<suffix>` | RBAC-enabled; all four apps granted Secrets User + Certificate User |
| Log Analytics Workspace | `log-woodgrove-<env>` | |
| Application Insights | `appi-woodgrove-<env>` | Workspace-based |
| Azure Communication Services | `acs-woodgrove-<env>-<suffix>` | Linked to Azure-managed email domain |
| ACS Email Service | `email-woodgrove-<env>-<suffix>` | Azure-managed `@azurecomm.net` domain (dev/test) |

All four web apps use **system-assigned managed identity**; `WEBSITE_LOAD_CERTIFICATES=*` ensures client certs are available in the Windows cert store.  
Secrets are Key Vault references (`@Microsoft.KeyVault(SecretUri=...)`); non-secret config comes from `main.bicepparam`.

---

## Prerequisites

```bash
az --version          # ≥ 2.50.0
az bicep version      # ≥ 0.24.0  (az bicep upgrade if older)
az login              # sign in with your corporate account
az account set --subscription "<subscription-id>"
```

---

## Deploy

### 1 — Fill in `main.bicepparam`

Open `infra/main.bicepparam` and replace every `<placeholder>` with real values:

| Placeholder | Where to find it |
|---|---|
| `<your-entra-external-id-tenant-id>` | Azure portal → Entra External ID → Overview → Tenant ID |
| `<web-app-client-id>` | Entra → App registrations → woodgrove-groceries → Overview |
| `<api-app-client-id>` | Entra → App registrations → woodgrove-groceries-api → Overview |
| `<graph-middleware-client-id>` | Entra → App registrations → graph-middleware → Overview |
| `<auth-api-client-id>` | Entra → App registrations → auth-api → Overview |
| `<tenant-subdomain>` | Your CIAM tenant subdomain (e.g. `woodgrovedemo`) |
| `<cloudflare-zone-id>` | Cloudflare dashboard → your domain → Zone ID |

**No secrets go in the param file.** Client secrets / connection strings are seeded to Key Vault in step 4.

### 2 — Validate (what-if)

```bash
az deployment sub what-if \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam
```

### 3 — Deploy

```bash
az deployment sub create \
  --name woodgrove-deploy-dev \
  --location eastus2 \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam
```

The deployment creates the resource group, then provisions all resources in dependency order (monitoring and App Service Plan first, web apps next, then Key Vault with RBAC assignments, then ACS).

Capture the outputs — you will need them in the next steps:

```bash
az deployment sub show \
  --name woodgrove-deploy-dev \
  --query properties.outputs
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

# 3 — Web app client secret (from Entra app registration → Certificates & secrets)
az keyvault secret set --vault-name $KV_NAME \
  --name web-client-secret --value "<paste-client-secret>"

# 4 — Cloudflare API token / secret
az keyvault secret set --vault-name $KV_NAME \
  --name cloudflare-api-secret --value "<paste-cloudflare-token>"
```

### Upload Client Certificates to Key Vault

```bash
# Upload each PFX certificate that the apps need to load from the Windows cert store.
az keyvault certificate import \
  --vault-name $KV_NAME \
  --name <cert-name> \
  --file /path/to/cert.pfx \
  --password "<pfx-password>"
```

After upload, App Service can sync the cert via the Key Vault Certificate User role already granted.  
In the App Service blade: **TLS/SSL Settings → Private Key Certificates (.pfx) → Import from Key Vault**.  
Once synced, the cert's thumbprint is loaded into the Windows cert store at runtime because `WEBSITE_LOAD_CERTIFICATES=*`.

---

## Post-Deploy: Configure Custom Domains

Each web app needs a custom domain and a TLS certificate:

```bash
# Example for the web app — repeat for api., graph-middleware., auth-api.
APP_NAME="app-woodgrove-web-dev-<suffix>"
RG="rg-woodgrove-dev"

# 1 — Add custom domain (DNS CNAME/A must already point to the app)
az webapp config hostname add \
  --webapp-name $APP_NAME \
  --resource-group $RG \
  --hostname woodgrovedemo.com

# 2 — Bind the uploaded cert to the custom domain
az webapp config ssl bind \
  --name $APP_NAME \
  --resource-group $RG \
  --certificate-thumbprint "<thumbprint>" \
  --ssl-type SNI
```

---

## Manual Entra External ID Steps (Bicep Cannot Do These)

These steps require an Entra administrator and must be completed after deployment.

### A — App Registrations (one per component)

In **Azure portal → Entra External ID → App registrations**, create four registrations and record the client IDs into `main.bicepparam`:

1. **woodgrove-groceries** (web)  
   - Platform: Web  
   - Redirect URIs: `https://woodgrovedemo.com/signin-oidc`  
   - Client secret → seed as `web-client-secret` in Key Vault  
   - API permissions: `openid`, `offline_access`, `profile`, your API scopes

2. **woodgrove-groceries-api**  
   - Platform: Web / API  
   - Expose API: define scopes used by the web front-end

3. **graph-middleware**  
   - Platform: Web  
   - API permissions: Microsoft Graph delegated/application scopes as required

4. **auth-api** (custom authentication extension)  
   - Platform: Web  
   - Redirect URI: `https://<authAppHostName>.azurewebsites.net/`

### B — Register the Custom Authentication Extension

1. Go to **Entra External ID → Custom authentication extensions → Create**.
2. Select event type: **TokenIssuanceStart** (or the appropriate event for your flow).
3. Set the **Target URL** to:
   ```
   https://<authAppHostName>.azurewebsites.net/api/CustomAuthenticationExtension
   ```
   Replace `<authAppHostName>` with the value from the deployment outputs.  
   > This is the stable public HTTPS endpoint — do NOT change the App Service default hostname unless you add a custom domain and keep it stable.
4. Select or create an associated app registration (the **auth-api** registration from step A-4).
5. Grant admin consent for the custom extension permissions.

### C — Authentication Flows

Wire up your Entra External ID user flows / custom policies to call the custom authentication extension registered in step B.

---

## Environment Teardown

```bash
# Removes the entire resource group (irreversible)
az group delete --name rg-woodgrove-dev --yes --no-wait
```

Soft-deleted Key Vaults must be purged to free the globally unique name:

```bash
az keyvault purge --name kv-wg-dev-<suffix> --location eastus2
```

---

## Module Reference

| File | Scope | Description |
|---|---|---|
| `main.bicep` | `subscription` | Orchestrator: creates RG, wires modules |
| `modules/appServicePlan.bicep` | resource group | Windows App Service Plan |
| `modules/webApp.bicep` | resource group | Reusable Windows web app (system identity, always-on, HTTPS, cert loading) |
| `modules/keyVault.bicep` | resource group | RBAC-enabled Key Vault; role assignments for each app's managed identity |
| `modules/monitoring.bicep` | resource group | Log Analytics Workspace + workspace-based Application Insights |
| `modules/communicationServices.bicep` | resource group | ACS + Email Service + Azure-managed domain |
