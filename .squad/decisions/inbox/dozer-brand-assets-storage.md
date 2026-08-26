## 2026-08-26: Private blob hosting for non-public brand assets

- **Decision:** Keep demo/customer brand assets out of this public repo and host them in a dedicated Azure Blob container provisioned by IaC.
- **Initial implementation:** Added `infra/modules/brandAssetsStorage.bicep` and wired `infra/main.bicep` to deploy a Storage Account + `brand-assets` container, inject `BrandAssets__BaseUrl`, and grant RBAC read/write roles to app identities and the deployer.
- **Why:** This keeps proprietary demo branding out of source control while matching the repo's existing IaC + managed-identity conventions.

## 2026-08-26: Access-model correction for login-page brand assets

- **Correction:** The earlier fully private-read model was wrong for this use case. Entra External ID branding images are fetched by the unauthenticated end user's browser before sign-in, so they must be anonymously readable by direct blob URL.
- **Updated implementation:** `infra/modules/brandAssetsStorage.bicep` enables `allowBlobPublicAccess=true` at the storage account and sets container `publicAccess=Blob`.
- **Security boundary:** Upload/write remains locked down with Entra RBAC (`Storage Blob Data Contributor` for the deployer identity). Anonymous access is limited to **reading individual blobs**; anonymous container listing remains disabled because the container is not set to `Container` access.
- **Policy:** This container is for public-facing non-sensitive brand assets only. Never upload secrets, internal docs, or any sensitive content.
