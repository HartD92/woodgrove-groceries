# Login branding design for issue #83

## Goal

Replace the neutral/default Entra External ID sign-in experience with a premium Abercrombie & Fitch-inspired demo theme while staying on the Microsoft-hosted flow.

## Brand direction

- **Primary colors:** `#343434` (black), `#223846` (rich blue)
- **Accent colors:** `#352f39`, `#908895`, `#eccabb`, `#814f4a`, `#6e3d47`
- **Typography:** uppercase heritage serif wordmark style (Garamond/Bodoni-like) paired with a clean sans-serif body font (Helvetica/Arial family)
- **Mood:** minimal, dark, premium, generous letter spacing, restrained accent usage

## Recommended implementation

Use **Microsoft Entra External ID Company Branding + custom URL domain** as the primary path, then layer in the repo's auth-extension `onPageRenderStart` override for the same assets so the hosted journey stays visually consistent.

## Implementation Status

The repository now includes the **code/config side** of the Abercrombie & Fitch demo theme:

- custom CSS: `src/storefront/wwwroot/Company-branding/af-custom.css`
- updated sign-in text: `src/storefront/wwwroot/Company-branding/login-text-en.md`, `src/storefront/wwwroot/Company-branding/login-text-de.md`
- configurable external-asset references via `BrandAssets__BaseUrl`

Implemented in code:

- `src/auth-api/Controllers/onPageRenderStartController.cs` now emits the Abercrombie & Fitch demo colors and `af-custom.css`, and it references external logo/background/favicons through `BrandAssets:BaseUrl` only when configured.
- `src/auth-api/Models/PageRenderStartResponse.cs` now exposes `headerBackgroundColor` so the response can carry the rich-blue header treatment.
- `src/auth-api/Controllers/OnOtpSendController.cs` and `src/api/Controllers/SendCodeController.cs` now use `BrandAssets:BaseUrl` for the email logo when available, and fall back to a text wordmark when it is not.
- `infra/scripts/Apply-CompanyBranding.ps1` now applies the same default branding to a live tenant through Microsoft Graph and pulls image assets from the blob-read-only brand-assets host (or an operator-provided local folder) instead of from committed repo files.
- `src/storefront/Areas/Help/Pages/CompanyBranding.cshtml` and `infra/README.md` now document the updated payload/automation path.

Still requires live-tenant configuration:

- upload the approved A&F image set to the blob-read-only `brand-assets` container and set `BrandAssets__BaseUrl`
- run `infra/scripts/Apply-CompanyBranding.ps1` against the target Entra External ID tenant with `OrganizationalBranding.ReadWrite.All`
- complete/validate the existing custom URL domain rollout (`login.woodgrovegroceries.com`) against the live Front Door + Entra tenant
- enable any tenant-only toggles that are still admin-center only, such as the Company Branding self-service password reset display option

## Brand Asset Hosting

This repo is public, so **no actual Abercrombie & Fitch image assets are committed here**. Host the approved files in the blob-read-only Azure Blob container Dozer provisioned and set one of the following at deployment time:

- `BrandAssets__BaseUrl`
- `BRAND_ASSETS_BASE_URL`

Current access model, re-verified against PR #85 branch `squad/84-afd-signup-authority-fix`:

- storage account `allowBlobPublicAccess: true`
- container `publicAccess: 'Blob'`
- anonymous GET works for known blob URLs; listing is still disabled

Expected files beneath that base URL:

- `af-logo.svg`
- `af-logo-light.svg`
- `af-background.jpg`
- `af-favicon.png`
- `af-headerlogo.png`
- `af-bannerlogo.png`
- `af-square-logo-light.png`
- `af-square-logo-dark.png`

Usage expectations:

- `onPageRenderStart` uses the SVG/PNG/JPG files for hosted sign-in page branding when `BrandAssets__BaseUrl` is configured.
- OTP email templates use `af-headerlogo.png` when `BrandAssets__BaseUrl` is configured; otherwise they render a text wordmark fallback.
- `infra/scripts/Apply-CompanyBranding.ps1` uses `af-bannerlogo.png`, `af-headerlogo.png`, `af-background.jpg`, `af-favicon.png`, `af-square-logo-light.png`, and `af-square-logo-dark.png` for Microsoft Graph uploads.
- If the base URL is unset, the app still builds and falls back to color/CSS/text-only branding instead of failing.

## Sources

- Brandfetch public Abercrombie & Fitch brand profile: <https://brandfetch.com/abercrombie.com>
- Wikimedia Commons logo page: <https://commons.wikimedia.org/wiki/File:Abercrombie_%26_Fitch_logo.svg>
- Microsoft Graph organizational branding reference: <https://learn.microsoft.com/en-us/graph/api/resources/organizationalbranding?view=graph-rest-1.0>
- Microsoft Graph update branding reference: <https://learn.microsoft.com/en-us/graph/api/organizationalbranding-update?view=graph-rest-1.0>
- Microsoft Graph update branding localization reference: <https://learn.microsoft.com/en-us/graph/api/organizationalbrandinglocalization-update?view=graph-rest-1.0>
