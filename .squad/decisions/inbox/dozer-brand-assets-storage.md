## 2026-08-26: Private blob hosting for non-public brand assets

- **Decision:** Keep demo/customer brand assets out of this public repo and host them in a private Azure Blob container provisioned by IaC.
- **Implementation:** Added `infra/modules/brandAssetsStorage.bicep` and wired `infra/main.bicep` to deploy a private Storage Account + `brand-assets` container with `allowBlobPublicAccess=false`, `allowSharedKeyAccess=false`, and container `publicAccess=None`.
- **Access model:** Storefront and auth-api managed identities receive `Storage Blob Data Reader`; the deployer identity receives `Storage Blob Data Contributor` so uploads can use Entra auth (`az login`) instead of account keys.
- **Config contract:** Deployment computes the container URL and injects it as `BrandAssets__BaseUrl` (`BrandAssets:BaseUrl` in .NET config) for the storefront and auth-api apps.
- **Why:** This keeps proprietary demo branding out of source control while matching the repo's existing IaC + managed-identity conventions.
- **Caveat:** Because the container is private, direct browser-hosted branding URLs still need a reviewed proxy or signed-delivery layer before runtime code should point hosted pages at blob URLs.
