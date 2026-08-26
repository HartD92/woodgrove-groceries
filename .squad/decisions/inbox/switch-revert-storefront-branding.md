# 2026-08-26T14:46:43-07:00 — Switch — storefront branding boundary for issue #90

## Decision

Abercrombie & Fitch branding should stop at the Entra External ID hosted surfaces and not alter the Woodgrove Groceries storefront UI itself.

## What changed

- Reverted the storefront shell/layout changes from PR #86 by restoring the pre-theme `Index`, `_Layout`, and `_LoginPartial` files.
- Removed `src/storefront/wwwroot/css/af-theme.css` so the app no longer applies the A&F palette, typography, and shell styling.
- Kept the Entra Company Branding assets, docs, and automation (`src/storefront/wwwroot/Company-branding/*`, `docs/branding-design.md`, `infra/scripts/Apply-CompanyBranding.ps1`, and related brand-asset hosting config) unchanged because those belong to the hosted sign-in/sign-up experience, not the app shell.

## Why

The demo now has a clearer boundary: Company Branding handles the login experience, while the storefront remains the normal Woodgrove experience users see before and after hosted auth.
