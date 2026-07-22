# Squad Decisions

## 2026-07-21: Monorepo consolidation (consolidated) — Phase 1 Complete

**By:** Morpheus

**What:** Consolidated four separate GitHub repos (woodgrove-groceries, woodgrove-groceries-api, woodgrove-groceries-graph-middleware, woodgrove-auth-api) into a single monorepo under `src/{storefront,api,graph-middleware,auth-api}/` using git subtree to preserve full per-commit history. Created root solution `woodgrove.slnx` referencing all four projects.

**Status:** COMPLETE (branch squad/monorepo-consolidation, not pushed). Build verified: 0 errors, 36 pre-existing warnings.

**Implementation executed:**
1. Branched from `main` → `squad/monorepo-consolidation`.
2. Committed Squad scaffolding (`.github`, `.squad`, `.mcp.json`, `.copilot`, `.gitattributes`, `.gitignore`, `.vscode`).
3. Relocated storefront via `git mv` (history preserved): Areas, Controllers, Helpers, Models, Pages, Properties, wwwroot, appsettings.json, Program.cs, web.config, woodgrovedemo.csproj → `src/storefront/`.
4. Imported three external repos as git subtrees (no squash, full history preserved):
   - `src/api` ← `https://github.com/HartD92/woodgrove-groceries-api.git` @ main
   - `src/graph-middleware` ← `https://github.com/HartD92/woodgrove-groceries-graph-middleware.git` @ main
   - `src/auth-api` ← `https://github.com/HartD92/woodgrove-auth-api.git` @ main
5. Created root solution `woodgrove.slnx` (.NET 10 SDK default format; replaces planned `woodgrove.sln`).
6. Build verified: `dotnet restore` + `dotnet build -c Release` → 0 errors, 36 warnings (all pre-existing).

**Future phases (out of scope here):**
- `src/shared/` project for deduplicating common models
- CI boundary configuration per service
- Shared package version pinning / Directory.Packages.props

**Decision made:** Phase 1 complete. Proceed to Phase 2: shared project creation and package convergence.

---

## 2026-07-21: Modernization — .NET 10 LTS upgrade + framework packages + backend fixes (consolidated)

**By:** Tank, Trinity

**What:** Executed .NET 9.0 → 10.0 LTS upgrade across all four services + framework package convergence + critical backend bug fixes.

**Status:** COMPLETE (9 commits on squad/monorepo-consolidation, not pushed). Build: 0 errors, 12 NuGet warnings (pre-existing transitive CVEs in Microsoft.Graph / Microsoft.Identity.Web).

**.NET 10 upgrade:**
- All four projects: `<TargetFramework>net9.0</TargetFramework>` → `<TargetFramework>net10.0</TargetFramework>`
- Framework-tied packages all converged to 10.0.10: Microsoft.AspNetCore.OpenApi, Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.Extensions.Caching.Memory, Microsoft.Extensions.Logging.AzureAppServices
- Storefront package convergence: Microsoft.Graph 5.83.0 → 5.94.0; Microsoft.Identity.Web 3.9.4 → 3.14.1 (all 5 packages: +DownstreamApi, +GraphServiceClient, +UI)
- Breaking change fix: removed `using Microsoft.OpenApi.Models;` (removed from OpenApi 10.0 API surface) in src/api/Program.cs and src/auth-api/Program.cs

**Backend bug fixes (Tank):**
1. **Zero-GUID code invalidation (P0 — critical):** `src/api/Controllers/VerifyCodeController.cs` — `new Guid().ToString()` → `Guid.NewGuid().ToString()`. The former always produces all-zeros `00000000-0000-0000-0000-000000000000`; reused codes would match cached value. Fixed with cryptographically random generation.
2. **Stack trace destruction (P1):** `src/api/Controllers/SendCodeController.cs` — `throw ex;` → `throw;` in catch block. Bare `throw` preserves original stack trace; `throw ex` resets to catch site, losing diagnostic info.
3. **Missing [HttpGet] routes (P0 — 404s):** `src/storefront/Controllers/UserProfile/UserMoreInfoController.cs`, `UserRolesController.cs`, `src/storefront/Controllers/Identity/SignInController.cs` — added `[HttpGet]` to `GetAsync()` / `OnGetDefault()`. Under `[ApiController]` without explicit verb, methods return 404.
4. **BuildServiceProvider anti-pattern (P1):** `src/storefront/Program.cs` — removed `builder.Services.BuildServiceProvider().GetService<TelemetryClient>()` call. Creates second DI container; scoped services resolve to different instances. Changed to `_telemetry = app.Services.GetService<TelemetryClient>()` after `app.Build()`.
5. **Static HttpClient thread-safety bug (P1):** `src/storefront/Controllers/Demos/UserInsightsController.cs` — removed static `HttpClient` with mutable `DefaultRequestHeaders.Authorization` (race condition). Added `IHttpClientFactory` injection; create fresh client per request.
6. **Triple AddAuthentication() calls (hygiene):** `src/storefront/Program.cs` — removed two dead `AddAuthentication()` calls and large commented-out `.AddPolicyScheme("DynamicAuth")` block. Preserved three active schemes: OpenIdConnect, ArkoseFraudProtection, EmailOtp.

**Security fixes (Trinity):**
1. **Token leak (CRITICAL):** `src/auth-api/Controllers/Temporary/EchoController.cs` — removed entire Authorization header logging to Application Insights. Logged raw bearer tokens to telemetry storage (PII/token leakage risk).
2. **Committed real identity config (CRITICAL):** `src/graph-middleware/appsettings.Development.json` — `git rm --cached` to untrack. File remains on disk for local dev; covered by `.gitignore` patterns so won't re-commit. Created `appsettings.Development.json.template` with placeholder values (`<tenant-name>`, `<tenant-id>`, `<client-id>`, `<cert-thumbprint>`). History contains non-secret infrastructure identifiers; rotation optional but recommended.
3. **[Authorize] commented out (CRITICAL):** `src/auth-api` — OnTokenIssuanceStartController, OnAttributeCollectionStartController, OnAttributeCollectionSubmitController, OnPageRenderStartController, SignUpStartsTestController all have `//[Authorize]`. For demo/internal use on private App Service with network restrictions acceptable if consciously documented. Per-endpoint re-enable or network isolation model must be explicit.
4. **Graph scope inconsistency (non-blocking):** `src/graph-middleware/appsettings.json` — `GraphApi:Scopes` changed from bare `"User.ReadWrite"` to `"https://graph.microsoft.com/.default"` (correct for client-credential flow). Code already hardcodes `.default` as default; config now consistent.
5. **Temporary controllers gated to Development:** New `src/auth-api/Helpers/DevelopmentOnlyAttribute.cs` — IResourceFilter returns 404 when not in Development environment. Decorated EchoController and SignUpStartsTestController. Keeps routing config unchanged while preventing Production access.

**Non-blocking items (deferred to next sprint):**
- Hardcoded app IDs (auth-api) — move to config for per-environment overrides
- GraphServiceClient bypass + no token caching (graph-middleware) — refactor to singleton/scoped service with caching
- Mixed MSAL / Identity.Web token acquisition (graph-middleware) — consolidate token layer
- Logging filter may suppress infrastructure logs (auth-api) — broaden filter to allow `Warning`+ from all providers
- `throw ex;` in groceries-api SendCodeController (fixed as B2 above)
- Verification codes stored plaintext (TBD comments in SendCode / VerifyCode) — in-memory cache sufficient for demo; resolve before hardening

**Decision made:** All CRITICAL and P0 items fixed before deployment. Schedule P1 items for next sprint. Non-blocking deferred. Full solution builds with 0 errors on branch.

---

## 2026-07-21: Security audit + fixes (consolidated) — Branch Execution Complete

**By:** Trinity, Tank

**What:** Executed identity/auth/security audit and critical fixes on branch squad/monorepo-consolidation.

**Status:** COMPLETE (2 commits: 145ac05, a82df88, not pushed). Build: 0 errors, 47 pre-existing warnings (all in src/api, src/graph-middleware C# nullability; none frontend).

**CRITICAL fixes executed:**

1. **Token leak — EchoController bearer-token logging (FIXED):** `src/auth-api/Controllers/Temporary/EchoController.cs` logged raw `Authorization` header to Application Insights / ILogger. Removed entire authorization header inspection + logging block. Controller now logs only request body and method name.

2. **Committed real Entra identity config (FIXED):** `src/graph-middleware/appsettings.Development.json` tracked in git with real values (tenant URL, tenant GUID, client GUID, certificate thumbprint). Executed `git rm --cached` to remove from version control. File remains on disk for local dev; covered by `.gitignore` so will not re-commit. Created `appsettings.Development.json.template` with placeholder values. Real identifiers remain in history (non-secret infrastructure identifiers); rotation optional but recommended.

3. **Most [Authorize] commented out in auth-api (ACKNOWLEDGED):** OnTokenIssuanceStartController, OnAttributeCollectionStartController, OnAttributeCollectionSubmitController, OnPageRenderStartController, SignUpStartsTestController all have `//[Authorize]`. For demo/internal use on private App Service with network restrictions this may be acceptable only if consciously documented. Created new `src/auth-api/Helpers/DevelopmentOnlyAttribute.cs` IResourceFilter to gate Temporary controllers to Development environment only (returns 404 in Production). Decorated EchoController and SignUpStartsTestController. Per-endpoint `[Authorize]` re-enable or documented network isolation model required before hardening.

4. **Removed EchoController token logging and gated Temporary controllers:** See #1 above. Also applied `[DevelopmentOnly]` attribute to gate Temporary endpoints.

5. **Graph scope standardized to .default:** `src/graph-middleware/appsettings.json` `GraphApi:Scopes` changed from bare `"User.ReadWrite"` to `"https://graph.microsoft.com/.default"` (correct for client-credential flow). `appsettings.Development.json.template` also uses `.default`. Code already hardcodes `.default` as fallback; config now consistent.

**NON-BLOCKING items (deferred — schedule for next sprint):**
- **Hardcoded app IDs (auth-api):** `99045fe1-7639-4a75-9d4a-577b6ca3810f`, `7a30b8ed-...`, `65d59577-...` hardcoded in multiple places; move to appsettings.json for per-environment overrides.
- **GraphServiceClient bypass + no token caching (graph-middleware):** `ProfileController.cs:97` bypasses injected client, calls `MsalAccessTokenHandler.GetGraphClient()` directly per request re-reading cert from store. Wrap in singleton/scoped service with token caching; diagnose OBO config issue first.
- **Mixed MSAL / Identity.Web token acquisition (graph-middleware):** Both Microsoft.Identity.Client and Microsoft.Identity.Web imported in `MsalAccessTokenHandler`. Consolidate to single Identity.Web layer; remove or consolidate unused methods.
- **Logging filter suppresses infrastructure logs (auth-api):** `Program.cs:16–19` filter `provider!.ToLower().Contains("woodgroveapi")` passes only logs with "woodgroveapi" in provider name, dropping Microsoft.AspNetCore auth failure logs (critical for debugging). Allow `Warning`+ from all providers.
- **Verification codes stored plaintext (groceries-api):** `SendCodeController.cs:70`, `VerifyCodeController.cs:51` have TBD comments. In-memory cache sufficient for demo but resolve before hardening.

**Decision made:** CRITICAL fixes complete and committed to branch. Non-blocking deferred to next sprint with prioritization list.

---

## 2026-07-21: Frontend modernization — Bootstrap 5 migration + accessibility + library currency (consolidated)

**By:** Switch

**What:** Executed frontend audit and modernization on branch squad/monorepo-consolidation: Bootstrap 4→5 migration, 212 image alt texts + WCAG fixes, dead library removal, CDN version pinning.

**Status:** COMPLETE (3 commits: b1ae25f, 01f87f1, 5bc8b0f, not pushed). Build: 0 errors, 47 pre-existing warnings (all C# in backend, none frontend).

**Bootstrap 5 functional fixes:**
- Fixed `<lu>` typo → `<ul>` in `_Layout.cshtml` (lines 41-43) and `Areas/Help/Pages/RBAC.cshtml` (lines 235/240).
- Migrated data attributes: `data-toggle="popover"` → `data-bs-toggle="popover"`, `data-trigger` → `data-bs-trigger`, added `hover focus` for keyboard accessibility.
- Replaced jQuery popover plugin `$('.feedback').popover({...})` with vanilla Bootstrap 5 API `document.querySelectorAll('.feedback').forEach(el => new bootstrap.Popover(el, {...}))` in `wwwroot/js/site.js`. Fixed typo "Foud" → "Found" in popover content.
- Removed/replaced Bootstrap 4–only classes: `form-group` → `mb-3` (12× in Profile.cshtml), `text-left` → `text-start` (8× in Profile.cshtml), `font-weight-bold` → `fw-bold` (Index.cshtml). Updated CSS rule `.form-group` → `.mb-3`.
- Fixed SVG xlink issues in Index.cshtml waves: removed `xmlns:xlink` namespace, replaced `xlink:href="#gentle-wave"` → `href="#gentle-wave"` (4×), fixed `fill="rgba(255,255,255,0.7"` → `fill="rgba(255,255,255,0.7)"` (added missing closing paren).
- Fixed Chart.js script load order: moved from outside `@section Scripts` into the section; pinned to `@4.4.9`.
- Removed redundant `role="main"` on `<main>` element (semantic HTML5 element carries this implicitly).

**Accessibility (WCAG) — 212 images + controls:**
- **Alt text:** Added meaningful alt text to 212 images: 204 in Areas/Help/Pages/*.cshtml (28 files) with pattern `"Screenshot of <humanized filename>"`; 5 in Pages/Commercial.cshtml; 3 in Pages/Help.cshtml.
- **Placeholder image:** `_Layout.cshtml` empty `src=""` imagepreview → transparent 1×1 data-URI to suppress spurious browser request; added `alt=""` (decorative, correctly empty).
- **Keyboard-accessible allergy warning:** `Pages/Index.cshtml` `<div class="card-allergy" role="button">` → `<button type="button" class="card-allergy">` with `data-bs-trigger="hover focus"` for keyboard users.
- **Decorative SVG aria-hidden:** `_Layout.cshtml` footer SVG and `Index.cshtml` waves SVG both added `aria-hidden="true" focusable="false"`.
- **Decorative footer icons:** `_Layout.cshtml` added `aria-hidden="true"` to 5 decorative `<i class="bi bi-*">` footer icons.
- **Spinner loading text:** `Dashboard.cshtml` and `Profile.cshtml` added `<span class="visually-hidden">Loading…</span>` inside all 12 `role="status"` spinners (DailyActiveUsers, MonthlyActiveUsers, NewUsers, Requests, MonthlyAuthentications, AuthenticationsPerCountry, OperationSystemsOfAuthenticationRequests, editProfile, roles, signIn, mfa, verification).

**Dead assets + CDN pinning:**
- **Deleted:** `wwwroot/lib/bootstrap/` (Bootstrap 5.1.0, never referenced); `wwwroot/lib/jquery/dist/` (jQuery 3.5.1, CVE-2020-11022 / CVE-2020-11023, never referenced).
- **Retained:** `wwwroot/lib/jquery-validation/` + `wwwroot/lib/jquery-validation-unobtrusive/` (actively used by `_ValidationScriptsPartial.cshtml`).
- **CDN version pins (_Layout.cshtml):** Bootstrap CSS 5.3.3→5.3.6 (with SRI hash), Bootstrap JS 5.3.3→5.3.6 (with SRI), Bootstrap Icons 1.11.1→1.13.1 + crossorigin, bs-stepper floating npm → @1.2.0 + crossorigin, Chart.js floating npm → @4.4.9/dist/chart.umd.min.js + crossorigin. Deferred SRI hashes marked with `<!-- TODO: add SRI hash -->` (compute via `curl | openssl dgst -sha384 | openssl base64`).

**Grep verification (0 remaining Bootstrap 4 patterns in src/storefront):**
- `data-toggle=` → 0 hits
- `font-weight-bold` → 0 hits
- `text-left` → 0 hits
- `<lu>` / `</lu>` → 0 hits
- `xlink:href` → 0 hits

**Decision made:** Bootstrap 5 migration complete. Accessibility baseline established (212 images + 12 spinners). CDN pins updated with SRI deferred to separate task (compute hashes + commit).

## 2026-07-21: Azure deployment topology & Bicep IaC — Windows App Service (consolidated)

**By:** Dozer

**What:** Authored full Azure Bicep IaC for deploying all four services (woodgrove-groceries, woodgrove-groceries-api, woodgrove-groceries-graph-middleware, woodgrove-auth-api) to Azure App Service on Windows (not Container Apps).

**Status:** COMPLETE — Bicep IaC committed to main branch (commit 509274c). `az bicep build infra/main.bicep` passes with **zero errors, zero warnings** (Bicep CLI 0.44.1).

**Deployment topology:** Four App Services with single App Service Plan (P1v3), Key Vault for cert thumbprints and secrets, system-managed identities per App Service, certificate loading via `WEBSITE_LOAD_CERTIFICATES`, all four services internet-reachable with public HTTPS endpoints.

**Why Windows App Service:**
- No Dockerfiles exist; containerization adds zero functional benefit.
- web.config already present with ASP.NET Core Module V2 in-process hosting — App Service is natural deployment target.
- Windows App Service supports Windows certificate store (CurrentUser/My) used by all cert-loading services.
- App Service logging already wired (builder.Logging.AddAzureWebAppDiagnostics()) in three services.
- Custom domains + managed TLS first-class in App Service (required for woodgrovedemo.com domain enforcement in web.config).
- All four services are stateless; no database, no VNet, no Front Door, no CDN needed.
- All four services must be internet-reachable (woodgrove-auth-api called by Entra External ID cloud service; other three called by web app over internet).

**Bicep architecture:**
- **Scope:** `targetScope = 'subscription'` — `main.bicep` creates resource group itself (azd-friendly pattern).
- **Deployment order:** Apps deployed before Key Vault so system-assigned managed identity `principalId` outputs are available for RBAC role assignments. Bicep infers dependency automatically from output references.
- **Key Vault URI:** Computed as Bicep `var` using `environment().suffixes.keyvaultDns` (sovereign-cloud safe) so app settings populated with KV references at deploy time; secrets resolve at runtime once KV seeded.
- **Secret patterns:** App settings use `@Microsoft.KeyVault(SecretUri=<versionless-uri>)` format (versionless URIs always resolve to latest version, avoiding rotation pain). Non-secret config (client IDs, tenant IDs, endpoint URLs) from Bicep parameters.
- **Certificate loading:** `WEBSITE_LOAD_CERTIFICATES=*` injected by `webApp.bicep` module into every app so Windows cert store populated at runtime.
- **ACS email:** Email service → Azure-managed domain → ACS linked in same module, provides working `@azurecomm.net` OTP sender out of the box for dev/test (TODO comment marks swap point for production custom domain).

**Files created:**
- `infra/main.bicep` — Subscription-scoped orchestrator; creates RG, deploys all modules
- `infra/modules/appServicePlan.bicep` — Windows App Service Plan (parameterized SKU, default P1v3)
- `infra/modules/webApp.bicep` — Reusable Windows web app module (system-assigned identity, httpsOnly, alwaysOn, ANCM in-process config)
- `infra/modules/keyVault.bicep` — RBAC-enabled Key Vault; loops over `appPrincipalIds` to grant Secrets User + Certificate User roles
- `infra/modules/monitoring.bicep` — Log Analytics Workspace (PerGB2018) + workspace-based Application Insights
- `infra/modules/communicationServices.bicep` — ACS + Email Service + Azure-managed domain
- `infra/main.bicepparam` — Parameter file (placeholders only, **no secrets**)
- `infra/README.md` — Deploy guide, KV seeding steps, custom domain config, Entra manual steps

**Azure resource list (parameterized by `env`: dev/staging/prod):**
- Resource Group: rg-woodgrove-{env}
- App Service Plan: asp-woodgrove-{env} [P1v3 Windows, single plan for all 4 apps]
- App Services (4): app-woodgrove-web-{env}, app-woodgrove-api-{env}, app-woodgrove-graph-{env}, app-woodgrove-auth-{env}
  - Custom domains: woodgrovedemo.com (web), api.woodgrovedemo.com (api), graph-middleware.woodgrovedemo.com (graph), auth-api.woodgrovedemo.com (auth)
  - System-assigned managed identities for each
- Key Vault: kv-woodgrove-{env}
  - Secrets: ApplicationInsights--ConnectionString, AzureAd--web--CertThumbprint, ArkoseFraudProtection--CertThumbprint, EmailOtp--CertThumbprint, MicrosoftGraph--CertThumbprint, GroceriesApi--Email--ConnectionString, AuthApi--Email--ConnectionString, AuthApi--CloudflareSecret, GraphMiddleware--AzureAd--CertThumbprint
- Application Insights: appi-woodgrove-{env} (shared workspace for cross-service correlation)
- Log Analytics Workspace: law-woodgrove-{env}
- Azure Communication Services: acs-woodgrove-{env} (email OTP for groceries-api and auth-api)

**Manual steps required post-deploy (not automatable by Bicep — require Entra admin):**
1. Create four Entra External ID app registrations; copy client IDs into `main.bicepparam`
2. Seed secrets into Key Vault: ApplicationInsights--ConnectionString, AzureAd--web--CertThumbprint, ArkoseFraudProtection--CertThumbprint, EmailOtp--CertThumbprint, MicrosoftGraph--CertThumbprint, GroceriesApi--Email--ConnectionString, AuthApi--Email--ConnectionString, AuthApi--CloudflareSecret, GraphMiddleware--AzureAd--CertThumbprint
3. Upload client certificates to Key Vault; sync to App Service
4. Register custom authentication extension in Entra pointing to `https://<authAppHostName>.azurewebsites.net/api/CustomAuthenticationExtension`
5. Configure custom domains + TLS bindings for each app

**Decision made:** Bicep IaC complete and committed. Ready for `az deployment sub create` CI/CD integration (deferred as open item). Staging slots and Private Endpoint for KV also deferred.

---

## 2026-07-21: PR #1 Code Review — OpenAPI + SRI Hash Fixes (consolidated)

**By:** Tank, Switch

**What:** Addressed code-review blockers and majors from PR #1 (squad/monorepo-consolidation) review: removed unused Microsoft.AspNetCore.OpenApi transitive vulnerability, corrected all SRI hashes for CDN resources.

**Status:** COMPLETE (2 commits: b0579c2 OpenAPI fix, 2157902 SRI fix, both pushed to PR #1).

### Fix 1: Remove Microsoft.AspNetCore.OpenApi from api + auth-api (Tank — Commit b0579c2)

**Blocker:** `Microsoft.AspNetCore.OpenApi 10.0.10` (scaffolding residue from .NET 10 upgrade) transitively pulls `Microsoft.OpenApi 2.0.0` affected by **GHSA-v5pm-xwqc-g5wc (high severity, NU1903)**. Bicep verification + code inspection confirmed neither `src/api` nor `src/auth-api` calls `AddOpenApi()`, `MapOpenApi()`, `AddSwaggerGen()`, `UseSwagger()`, or references any `Microsoft.OpenApi.*` types at runtime.

**Fix:** Removed `Microsoft.AspNetCore.OpenApi 10.0.10` PackageReference from both `src/api/woodgrove-groceries-api.csproj` and `src/auth-api/woodgroveapi.csproj`. No live usage; cleanest fix is direct reference removal (no pinning, no workarounds).

**Effect:** NU1903 for `Microsoft.OpenApi` no longer appears in build output. Build: **0 errors, 0 regressions**.

**Note:** Separate NU1903 for `Microsoft.Kiota.Abstractions 1.17.1` still present transitively via `Microsoft.Graph` in `src/graph-middleware` and `src/storefront` (out of scope for this batch — flagged as known follow-up for Dozer/Morpheus).

### Fix 2: SRI Hash Corrections — Bootstrap 5.3.6 + Full SRI Coverage (Switch — Commit 2157902)

**Major blocker:** `src/storefront/Pages/Shared/_Layout.cshtml` referenced Bootstrap CDN at version 5.3.6 but carried **stale 5.3.3 `integrity` hashes**. SRI hash mismatch causes browsers to silently block resources — site rendered unstyled, all Bootstrap JS components (navbar toggler, dropdowns, modals, offcanvas, collapse) non-functional in production.

Additionally, three resources had `crossorigin="anonymous"` but missing `integrity` attributes (marked as TODO).

**Fix:** All five CDN resources independently verified via exact-URL download + SHA-384 hash computation (`Get-FileHash -Algorithm SHA384` hex → base64):

| Resource | Version | Verified SRI Hash |
|---|---|---|
| Bootstrap CSS | 5.3.6 | `sha384-4Q6Gf2aSP4eDXB8Miphtr37CMZZQ5oXLH2yaXMJ2w8e2ZtHTl7GptT4jmndRuHDT` |
| Bootstrap JS | 5.3.6 | `sha384-j1CDi7MgGQ12Z7Qab0qlWQ/Qqz24Gc6BM0thvEMVjHnfYGF0rmFCozFSxQBxwHKO` |
| Bootstrap Icons | 1.13.1 | `sha384-Bk5cbLkZQ5raZ0+H2/+VbfYx3WpvxvQK4zqXZr7sYODuaX7bKXoSOnipQxkaS8sv` |
| bs-stepper | 1.2.0 | `sha384-6rRui8N04BM1IJLLpBOExgmKF3mJy542qlJRq5cFlE68NLgVGCJHT0D/1tO0ozwN` |
| chart.js | 4.4.9 | `sha384-b0GXujLkk9eYYSmcSfoyZbfyElGAQnDyY0skCHSG6w3JgTMFnz11ggrTAr7seu9f` |

All URLs already at exact semver versions (no pinning needed).

**Files changed:**
- `src/storefront/Pages/Shared/_Layout.cshtml` — fixed Bootstrap CSS+JS hashes (stale 5.3.3→5.3.6), added SRI for bootstrap-icons + bs-stepper
- `src/storefront/Pages/Dashboard.cshtml` — added SRI for chart.js

**Validation:**
- `dotnet build src/storefront/woodgrovedemo.csproj -c Release` → **0 errors**
- `grep "TODO: add SRI"` → **no matches** (all TODOs resolved)

**Decision made:** PR #1 review blockers + majors resolved. Both commits pushed. Full solution builds 0 errors. Ready for approval and merge.

---

## 2026-07-21: Deploy workflow idempotency — web-client-secret management (consolidated)

**By:** Dozer

**What:** Made the `deploy-infra.yml` web-client-secret provisioning step idempotent with explicit opt-in rotation. Closes final PR #1 code-review item.

**Status:** COMPLETE (commit e3d8a6b, pushed to PR #1).

**Problem:** Post-deploy `az ad app credential reset --append` was called on **every** `infra/**` push where `provisionEntraApps=true`. Consequence: each push minted a new 1-year credential; previously-minted credentials remained valid until `endDateTime`, accumulating as orphaned-but-valid credentials on the storefront web app's Entra registration.

**Solution — idempotent secret management with opt-in rotation:**

| Condition | Behavior |
|---|---|
| `web-client-secret` exists in KV AND `rotate_web_secret=false` | **SKIP** — log message, take no action |
| `web-client-secret` absent (first provision) AND `provisionEntraApps=true` | **CREATE** — `az ad app credential reset --append`, mask password in logs, write to KV |
| `rotate_web_secret=true` (workflow_dispatch input) | **ROTATE** — force reset (`--append`), update KV, **prune expired credentials** defensively |

**Implementation details:**

- **KV existence check:** `az keyvault secret show` with `set +e` to capture exit code (exit 2 = "not found"; expected on first provision, not a failure)
- **Rotation timing:** `--append` ensures new credential active before old ones pruned — no window without a valid secret
- **Expired-cred pruning is defensive:** Failures in `az ad app credential list` skip pruning entirely (non-fatal). Parse errors on individual `endDateTime` entries skip that entry. Newly-created `keyId` never deleted
- **Gate condition broadened:** `env.PROVISION_ENTRA == 'true' || github.event.inputs.rotate_web_secret == 'true'` — rotation can be triggered even when `provisionEntraApps=false` (for pre-existing registrations)
- **Push-trigger normalization:** `${{ github.event.inputs.rotate_web_secret }}` is empty on push; shell `${ROTATE:-false}` normalizes to `false`

**Rationale:** Infrastructure deployments should be idempotent. A secret already in Key Vault and not near expiry does not need regeneration on every apply. Rotation is deliberate, operator-initiated action — not a side-effect of `git push`.

**Decision made:** Deploy workflow is now idempotent. Secret provisioned once on first deploy; rotated only via explicit workflow_dispatch input. PR #1 code-review items fully resolved. Commit e3d8a6b, pushed.

---

## 2026-07-21: Governance — branching & merge policy

**By:** David Hart (via Copilot coordinator)

**What:** Established branching and merge policy: all work happens in feature branches, merged to main via **squash-merge PRs only**. No direct commits to main.

**Why:** User directive. Establishes clean, reviewable history and avoids the earlier pattern of direct main commits (e.g., initial Bicep commit 509274c). Applies to all agents and coordinator going forward.

**Status:** POLICY ADOPTED effective immediately.

---

## Active Decisions

### Cross-project version consistency

**Decision:** Standardize Microsoft.Identity.Web to 3.14.1 across all projects (woodgrove-groceries, groceries-api, graph-middleware, auth-api) as part of monorepo migration and .NET 10 upgrade.

**Rationale:** Current split (woodgrove-groceries 3.9.4 vs api 3.14.1) causes token-handling divergence between projects that communicate over the wire. Unified version prevents silent incompatibilities and simplifies debugging.

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction

### 2026-07-21T11:55:23-07:00: Cross-tenant CI/CD two-identity split (consolidated)

**By:** Trinity, Dozer, Coordinator, David Hart (approval)

**What:** Supersede the earlier single-identity MS Graph Bicep deployment model for Entra app registrations. The Azure subscription remains in the workforce tenant, while the CIAM directory is a separate Microsoft Entra External ID tenant. The deployment must use two identities and two jobs:
- **Identity A / zure-deploy**: workforce tenant GitHub OIDC principal runs subscription-scope ARM/Bicep for Azure resources with provisionEntraApps=false.
- **Identity B / ntra-provision**: ExtID tenant GitHub OIDC principal provisions the four CIAM app registrations/service principals through z ad / Microsoft Graph REST, then emits client IDs for the ARM job.

**Why:** A workforce-tenant federated identity credential cannot authenticate against the ExtID tenant token endpoint (AADSTS70021). The workforce↔ExtID link is billing/management only, not an authentication trust. The Microsoft Graph Bicep extension targets the tenant of the deployment principal; because subscription deployment authenticates to the workforce tenant, it cannot create CIAM registrations in the ExtID tenant.

**Implementation outcome:** Dozer implemented the split in PR #3 (commit c5372ff, stacked on #2). infra/main.bicepparam now sets provisionEntraApps=false; infra/modules/entraApps.bicep is retained only as deprecated authoritative spec; infra/README.md documents the two-tenant PowerShell bootstrap.

**Operational requirements:** Add GitHub variables ENTRA_CLIENT_ID and ENTRA_TENANT_ID for Identity B. Identity B needs Application.ReadWrite.All, AppRoleAssignment.ReadWrite.All, and DelegatedPermissionGrant.ReadWrite.All consented in the ExtID tenant. Identity A keeps the workforce subscription deployment role assignments.

**Refs:** Trinity inbox decision 	rinity-cross-tenant-cicd.md; coordinator inbox coordinator-two-identity-split.md; PR #3; commit c5372ff.

### 2026-07-21T11:55:23-07:00: Deploy guide shell language — PowerShell only

**By:** Dozer

**What:** All shell command examples in infra/README.md are PowerShell (pwsh / PowerShell 7+) only. Bash examples were removed so the guide is copy-paste-ready for Windows PowerShell usage.

**Patterns established:** Use file-based '@file.json' patterns for z --parameters / z --body JSON; use $() subexpressions when a variable is followed by :; parse Azure CLI JSON with ConvertFrom-Json instead of python3 or jq; use PowerShell backtick continuations for multi-line z commands.

**Validation:** Representative here-string, ConvertTo-Json, and interpolation patterns were verified with pwsh -NoProfile -Command.

**Refs:** Dozer inbox decision dozer-powershell-guide.md; PR #3.

---

### 2026-07-21T16:52:42-07:00: Deploy workflow Key Vault check resets LASTEXITCODE

**By:** Dozer

**What:** The deploy-infra workflow resets `$global:LASTEXITCODE = 0` after the expected first-run `az keyvault secret show` SecretNotFound path so GitHub Actions pwsh does not fail the step from the last native command exit code.

**Why:** The workflow logic correctly handled a missing `web-client-secret`, but the runner can exit with `$LASTEXITCODE` at step end. Resetting it makes the expected not-found path explicit and keeps first deployment green.

---

### 2026-07-21T16:52:42-07:00: Key Vault grants deployer Secrets Officer via deployer().objectId

**By:** Dozer

**What:** The Key Vault module grants the ARM deployment principal Key Vault Secrets Officer using Bicep `deployer().objectId`, and the workflow secret steps tolerate first-run not-found plus transient RBAC propagation.

**Why:** RBAC-enabled Key Vaults need the CI deployment service principal to read and write deployment-managed secrets. Binding to the actual deployer avoids hard-coding the service principal object ID while allowing `az keyvault secret show` and `set` to succeed.

---

### 2026-07-21T16:52:42-07:00: Correct Key Vault built-in role definition GUIDs

**By:** Dozer

**What:** Corrected the Key Vault Secrets User role definition ID to `4633458b-17de-408a-b874-0445c86b69e6` and Key Vault Certificate User to `db79e9a7-68ee-4b58-9aeb-b90e7c24fcba`.

**Why:** Typoed built-in role GUIDs caused Azure deployments to fail with `RoleDefinitionDoesNotExist` after the region and SKU quota blockers were cleared.

---

### 2026-07-21T16:52:42-07:00: SecurityControl Ignore tag exempts public endpoint policy

**By:** Dozer

**What:** Added `SecurityControl: Ignore` to the shared `allTags` object in `infra/main.bicep` so all deployed resources receive the policy exemption tag.

**Why:** This subscription policy disables public network access unless the resource has `SecurityControl=Ignore`. Central tagging keeps Key Vault reachable from the GitHub runner for data-plane secret writes and preserves intended public endpoints.

---

### 2026-07-22: Monorepo app deploy uses matrixed OIDC job

**By:** Dozer

**What:** Replaced the stale single-app portal-generated deployment workflow with one matrixed GitHub Actions job that publishes and deploys the web, API, auth, and graph services independently on ubuntu-latest. Each matrix leg logs into Azure through the proven azure-infra OIDC repo variables and resolves the tokenized App Service name from `rg-woodgrove-dev` by role infix before deployment.

**Why:** A matrix keeps the four app deployments DRY and parallel while avoiding hardcoded App Service suffix tokens or stale portal-generated secrets. Resolving the App Service name at deploy time keeps the workflow aligned with tokenized Bicep deployments and fails clearly if an expected app cannot be found.

---

### 2026-07-22: Storefront service endpoints use runtime App Service URLs

**By:** Dozer

**What:** Updated the storefront web app Bicep settings to emit the exact endpoint keys consumed by the application: `WoodgroveGroceriesApi__Endpoint`, `WoodgroveGroceriesAuthApi__Endpoint`, and `GraphApiMiddleware__Endpoint`. The values are derived from the existing tokenized App Service name variables and preserve the required trailing slash or `/profile` path contract.

**Why:** The previous `Api__BaseUrl`, `AuthApi__BaseUrl`, and `GraphMiddleware__BaseUrl` settings did not match any storefront configuration reads, causing the app to fall back to hardcoded demo domains instead of calling freshly deployed sibling services.

---

### 2026-07-22: Storefront canonical-domain rewrite removed; sibling endpoints remain deployment-supplied

**By:** Switch

**What:** Removed the storefront canonical-host rewrite from `src/storefront/web.config` so the site can serve under any Azure-assigned host. Storefront sibling service endpoints must still be supplied by deployment configuration rather than relying on checked-in demo-domain defaults.

**Why:** The canonical-domain rewrite blocked Azure-hosted validation. Runtime endpoint settings keep the public repo free of environment-specific hostnames while allowing each deployment to call its matching sibling services.

---

### 2026-07-22T18:20:00Z: Public repo deployment values stay out of committed config

**By:** David Hart

**What:** This is a public repository. Do not hardcode tenant IDs, CIAM subdomains, client IDs, or environment-specific values in committed application, workflow, or infra parameter files. Source them from GitHub Actions repository variables/secrets at deploy time; keep `infra/main.bicepparam` placeholders as placeholders.

**Why:** Public files must remain reusable and must not bake one tenant or environment into source. Deployment-time repo variables/secrets preserve portability while allowing Azure deployments to receive real values.

---

### 2026-07-22T18:49:00Z: External ID CIAM authority-only contract and IDW10503 signature (consolidated)

**By:** David Hart, Trinity

**What:** For Microsoft.Identity.Web + Entra External ID (CIAM), configure AzureAd with the Authority-only subdomain-root form `https://{subdomain}.ciamlogin.com/` plus `ClientId`, `ClientSecret`, and `CallbackPath`. Do not append `/{tenantId}/v2.0`, and do not set `AzureAd:TenantId`, `AzureAd:Domain`, or `AzureAd:Instance`. `IDW10503` on the `/signin-oidc` callback during auth-code redemption indicates a CIAM authority-shape/cloud-instance resolution problem, not a redirect URI issue; redirect URI mismatches fail earlier as `AADSTS50011` at the authorize endpoint.

**Why:** Microsoft.Identity.Web v3.14.1's CIAM token-acquisition path resolves the tenant/cloud instance from the `ciamlogin.com` subdomain automatically. The workforce-style authority plus `TenantId`/`Domain` broke cloud-instance resolution; PR #21 restored the deployed storefront to the known-good CIAM contract and sign-in was verified end-to-end.

---

### 2026-07-22T18:20:00Z: Cloudflare API key not required by app code

**By:** David Hart

**What:** Do not require a Cloudflare API key for the application deployment path. The current .NET codebase has no `.cs` consumers for a Cloudflare API key; related demos are implemented as external Cloudflare-dashboard WAF rules.

**Why:** Treating Cloudflare API credentials as required would add unnecessary secret handling and deployment coupling for functionality that is not consumed by the app code.

---

### 2026-07-22T18:20:00Z: Deploy-infra workflow push trigger can collide with manual runs

**By:** David Hart

**What:** Merging changes to `.github/workflows/deploy-infra.yml` automatically triggers the deploy-infra workflow because the workflow file is inside its push path filter alongside `infra/**`. Avoid also triggering a manual `workflow_dispatch` for the same revision.

**Why:** Concurrent deploy-infra runs can collide on Azure nested deployments, especially ACS, and fail with `DeploymentActive`. Let the push-triggered run finish before starting any manual redeploy.
