# 2026-08-26T12:17:28.998-07:00 — Trinity — login branding recommendation for issue #83

## Decision / recommendation

For issue #83, use **Microsoft Entra External ID Company Branding plus a branded custom URL domain** as the primary solution for removing the default Microsoft/Entra sign-in feel. Keep the app on the supported Microsoft-hosted browser-delegated flow, and use `login.woodgrovegroceries.com` (via External ID custom URL domains + Azure Front Door) as the customer-facing auth host.

## Why

- Microsoft Learn confirms External ID customer tenants already use a neutral hosted page and support logos, background, footer/header, sign-in text, localization, and custom CSS.
- Microsoft Learn does **not** provide a hosted-page equivalent to Azure AD B2C custom HTML/JavaScript templates for External ID customer tenants.
- This repo already has the right ingredients: a Woodgrove branding asset bundle, a custom-domain path in infra/app settings, and an existing `onPageRenderStart` branding override in `src/auth-api`.
- A custom domain changes the browser address bar and is the highest-impact trust/branding improvement short of a full native-auth redesign.

## Guardrails

- Do not redesign around Azure AD B2C custom policies for this issue.
- Do not replace the repo's proven CIAM authority contract casually; validate custom-domain behavior against the existing redirect-time domain override path first.
- Treat native authentication as a later escalation only if supported hosted branding still fails the demo goal.

---

# 2026-08-26T12:34:19-07:00 — Trinity — Abercrombie & Fitch branding implementation for issue #83

## What I implemented

- Sourced a public Abercrombie & Fitch wordmark asset from Brandfetch and checked in the minimal file set needed for the demo (af-logo.svg plus a light derivative for dark-header usage).
- Generated Company Branding companion assets that satisfy Entra upload constraints: banner/header PNGs, square logos, favicon, and a 1920x1080 background image.
- Updated `src/auth-api/Controllers/onPageRenderStartController.cs` so the custom auth extension now emits the A&F-aligned colors, CSS, logo URLs, square logos, favicon, and sign-in copy.
- Added `infra/scripts/Apply-CompanyBranding.ps1` so a live tenant can apply the same default Company Branding package through Microsoft Graph.
- Updated docs/help content to point at the new automation path and the live-tenant boundary.

## What still needs live tenant access

- Run `infra/scripts/Apply-CompanyBranding.ps1` against the target External ID tenant with `OrganizationalBranding.ReadWrite.All`.
- Finish and validate the custom URL domain association (`login.woodgrovegroceries.com`) in Entra External ID + Azure Front Door.
- Enable any admin-center-only toggles that Graph still does not expose, especially **Show self-service password reset** on the sign-in form if desired for the demo.

## Notes

- The hosted-flow branding is now represented in repo assets/code, but tenant-side Company Branding is still a live-service operation.
- Local validation was limited by environment drift: the installed SDK is .NET 9.0.317, while the affected projects target `net10.0`.

---

# 2026-08-26T12:45:25-07:00 — Trinity — public-repo brand asset correction for issue #83

## Change in direction

Because woodgrove-groceries is public, no actual Abercrombie & Fitch image/logo files should live in git history for the working branch.

## What I changed

- Removed the previously added A&F SVG/PNG/JPG assets from the branch.
- Kept the theme code in-repo (f-custom.css, sign-in text, Graph/app wiring).
- Updated auth-page branding and OTP email branding to resolve image URLs from BrandAssets__BaseUrl or BRAND_ASSETS_BASE_URL and to fall back to text-only wordmark treatment when unset.
- Updated infra/scripts/Apply-CompanyBranding.ps1 to fetch branding images from the private brand-assets host (or an operator-provided local folder) instead of from committed repo files.
- Documented the required blob filenames and hosting contract in docs/branding-design.md and docs/brand-asset-hosting.md.

## Operational boundary

The live tenant still needs approved A&F assets uploaded to the private blob container before the full branded experience can render or be pushed into Entra Company Branding via Graph.
