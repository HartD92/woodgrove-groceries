### 2026-07-22: Storefront service endpoints use runtime App Service URLs

**By:** Dozer

**What:** Updated the storefront web app Bicep settings to emit the exact endpoint keys consumed by the application: `WoodgroveGroceriesApi__Endpoint`, `WoodgroveGroceriesAuthApi__Endpoint`, and `GraphApiMiddleware__Endpoint`. The values are derived from the existing tokenized App Service name variables and preserve the required trailing slash or `/profile` path contract.

**Why:** The previous `Api__BaseUrl`, `AuthApi__BaseUrl`, and `GraphMiddleware__BaseUrl` settings did not match any storefront configuration reads, causing the app to fall back to hardcoded demo domains instead of calling freshly deployed sibling services.
