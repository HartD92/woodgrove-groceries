# Public-read brand-asset hosting

Brand assets for demo-specific or customer-specific branding must stay **out of this public repository**. This repo provisions a dedicated Azure Blob container and passes its base URL into the deployed apps through `.NET` configuration as `BrandAssets__BaseUrl`.

## What gets deployed

`infra/main.bicep` now deploys:

- a dedicated Storage Account for brand assets
- a blob container named `brand-assets`
- `Storage Blob Data Reader` RBAC for the storefront and auth-api managed identities
- `Storage Blob Data Contributor` RBAC for the deployer identity, so CI/CD or an authorized operator can upload assets with Microsoft Entra auth

The current PR #85 branch configuration enables **anonymous blob-level read** while still blocking shared-key auth and container listing:

- `allowBlobPublicAccess = true`
- `allowSharedKeyAccess = false`
- container `publicAccess = Blob`

That means:

- direct GETs for known blob URLs such as `{BrandAssets__BaseUrl}/af-logo.svg` work for browser-hosted sign-in pages and email clients
- container enumeration is still disabled
- uploads still use Azure AD auth via RBAC

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

The container is intentionally **blob-read-only** for anonymous callers, not fully public. Callers can fetch a blob only if they already know its exact URL; listing remains disabled.

Do **not** commit new customer/demo brand files to this repo. Upload approved assets to the container instead, and treat the blob names above as part of the deployment contract.

## Related files

- `infra/main.bicep`
- `infra/modules/brandAssetsStorage.bicep`
- `.github/workflows/deploy-infra.yml`
- `src/storefront/appsettings.json`
- `src/auth-api/appsettings.json`
