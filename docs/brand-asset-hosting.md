# Brand-asset hosting

Brand assets for demo-specific or customer-specific branding must stay **out of this public repository**. This repo provisions a dedicated Azure Blob container and passes its base URL into the deployed apps through `.NET` configuration as `BrandAssets__BaseUrl`.

## What gets deployed

`infra/main.bicep` now deploys:

- a dedicated Storage Account for brand assets
- a blob container named `brand-assets`
- `Storage Blob Data Reader` RBAC for the storefront and auth-api managed identities
- `Storage Blob Data Contributor` RBAC for the deployer identity, so CI/CD or an authorized operator can upload assets with Microsoft Entra auth

The access model is intentionally split:

- **writes/uploads:** locked down with Entra RBAC
- **reads of individual asset URLs:** anonymous/public by design so the pre-auth sign-in page can load images in the end user's browser

The storage account/container settings are:

- `allowBlobPublicAccess = true`
- `allowSharedKeyAccess = false`
- container `publicAccess = Blob`

That means:

- direct GETs for known blob URLs such as `{BrandAssets__BaseUrl}/af-logo.svg` work for browser-hosted sign-in pages and email clients
- anonymous **blob GET** is allowed
- anonymous **container listing** is still disabled
- do **not** store anything sensitive in this container

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

GitHub Actions runs `infra/main.bicep`, which derives the storage account name and container URL, then sets `BrandAssets__BaseUrl` on the deployed apps. The deploy identity also receives blob-contributor RBAC on the container so uploads can use `az login` / OIDC instead of account keys.

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

## Read model for Entra sign-in branding

Branding images used on the Entra External ID sign-in page load in the **unauthenticated user's browser** before sign-in completes. Because of that, the image URLs themselves must support plain anonymous GET.

This repo therefore uses the standard low-risk pattern for login-page assets:

- upload locked down with RBAC
- anonymous read for individual blobs only
- no anonymous container enumeration

Do **not** commit new customer/demo brand files to this repo, and do **not** upload secrets, internal-only documents, or anything sensitive to this container.

## Related files

- `infra/main.bicep`
- `infra/modules/brandAssetsStorage.bicep`
- `.github/workflows/deploy-infra.yml`
- `src/storefront/appsettings.json`
- `src/auth-api/appsettings.json`
