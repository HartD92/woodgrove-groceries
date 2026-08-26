# Private brand-asset hosting

Brand assets for demo-specific or customer-specific branding must stay **out of this public repository**. This repo now provisions a private Azure Blob container and passes its base URL into the deployed apps through `.NET` configuration as `BrandAssets__BaseUrl`.

## What gets deployed

`infra/main.bicep` now deploys:

- a private Storage Account dedicated to brand assets
- a private blob container named `brand-assets`
- `Storage Blob Data Reader` RBAC for the storefront and auth-api managed identities
- `Storage Blob Data Contributor` RBAC for the deployer identity, so CI/CD or an authorized operator can upload assets with Microsoft Entra auth

The storage account disables anonymous blob access and shared-key auth:

- `allowBlobPublicAccess = false`
- `allowSharedKeyAccess = false`
- container `publicAccess = None`

## Runtime configuration

The deployment computes the container URL and injects it into App Service settings as:

- `BrandAssets__BaseUrl`

That maps to:

- `BrandAssets:BaseUrl` in `appsettings.json`

Current wiring:

- `src/storefront/appsettings.json`
- `src/auth-api/appsettings.json`
- `infra/main.bicep` app settings for the storefront and auth-api web apps

## CI/CD flow

No secret pipeline variable is required for the base URL.

GitHub Actions runs `infra/main.bicep`, which derives the storage account name and container URL, then sets `BrandAssets__BaseUrl` on the deployed apps. The deploy identity also receives blob-contributor RBAC on the container so uploads can use `az login` / OIDC instead of account keys or SAS tokens.

## Uploading assets

After infrastructure deployment, upload assets with Azure AD auth:

```powershell
$StorageAccount = "<brand-assets-storage-account-name>"
$Container = "brand-assets"
$SourceFolder = "<local-folder-containing-approved-brand-files>"

az storage blob upload-batch `
  --auth-mode login `
  --account-name $StorageAccount `
  --destination $Container `
  --source $SourceFolder `
  --overwrite
```

Example files you might upload:

- `headerlogo.png`
- `bannerlogo.png`
- `favicon.png`
- `background.jpeg`
- `customcss.css`

For the issue #83 Abercrombie & Fitch demo theme, the expected blob names are:

- `af-logo.svg`
- `af-logo-light.svg`
- `af-background.jpg`
- `af-favicon.png`
- `af-headerlogo.png`
- `af-bannerlogo.png`
- `af-square-logo-light.png`
- `af-square-logo-dark.png`

## Important limitation

This container is intentionally **private**. `BrandAssets__BaseUrl` is therefore best treated as the canonical asset source for **server-side** reads or for a future signed/proxied delivery path.

Do **not** commit new customer/demo brand files to this repo. If a flow needs to emit directly browser-fetchable branding URLs, add a reviewed proxy or signed-delivery mechanism on top of this private container rather than making the container public.

## Related files

- `infra/main.bicep`
- `infra/modules/brandAssetsStorage.bicep`
- `.github/workflows/deploy-infra.yml`
- `src/storefront/appsettings.json`
- `src/auth-api/appsettings.json`
