# Squad Decisions

## 2026-07-21: Monorepo consolidation (consolidated)

**By:** Morpheus

**What:** Consolidate four separate GitHub repos (woodgrove-groceries, woodgrove-groceries-api, woodgrove-groceries-graph-middleware, woodgrove-auth-api) into a single monorepo under `src/{storefront,api,graph-middleware,auth-api}/` with a shared `src/shared/` project (Woodgrove.Shared.csproj) to eliminate duplicated models and helpers across all four services. Use `git subtree add --prefix` to preserve per-commit history (476 combined commits: 339+31+15+91) and create a unified `woodgrove.sln` at the root with `Directory.Build.props` enforcing consistent TFM, nullable, implicit usings, and pinned NuGet versions (Microsoft.Identity.Web 3.14.1, Microsoft.Graph 5.94.0).

**Why:** Single source of truth for shared models (SendCodeRequest, VerifyCodeRequest, SendCodeResponse, AuthMethodType, UserAttributes, MsalAccessTokenHandler, ActAsRequest) prevents silent divergence. Version consistency fixes current splits (Identity.Web 3.9.4 vs 3.14.1, Graph 5.83.0 vs 5.94.0). Cross-service refactors in one PR with unified review. Single dotnet build validates the entire system.

**Implementation roadmap:**
- Create proposed monorepo layout with src/{storefront,api,graph-middleware,auth-api,shared}/, .github/workflows/ (path filters per service), infra/, docs/, Directory.Build.props, woodgrove.sln at root.
- Use `git subtree add` without squash to preserve history; move existing storefront files into src/storefront/.
- Create Woodgrove.Shared.csproj with deduplicated Models/ (SendCodeRequest, VerifyCodeRequest, SendCodeResponse, AuthMethodType, AccountData, UserAttributes, ActAsRequest) and Helpers/ (MsalAccessTokenHandler parameterized by config key path).
- Update all four service projects to reference src/shared/Woodgrove.Shared.csproj.
- Create four GitHub Actions workflows with `paths:` filters to deploy only affected services (deploy-storefront.yml, deploy-api.yml, deploy-graph-middleware.yml, deploy-auth-api.yml).
- Migrate three sets of AZUREAPPSERVICE_* secrets from archived repos to the monorepo.
- Archive the three original repos (set read-only on GitHub after migration).

**Risks/mitigations:**
- CI path filter misconfiguration → re-deploying all services: carefully review each `paths:` filter; add smoke-test gates before deploy.
- GH Actions secrets migration: document before archiving original repos.
- Storefront has no GH Actions workflow: create as part of migration.
- Independent versioning lost: acceptable for this demo system; not a stated requirement.

**Decision made:** Proceed with monorepo consolidation via git subtree.

---

## 2026-07-21: Modernization — .NET 10 upgrade + framework packages (consolidated)

**By:** Tank, Trinity

**What:** Upgrade both woodgrove-groceries and woodgrove-groceries-api from `net9.0` (EOL 2026-05-12) to `net10.0` LTS (supported until 2028-11). Update all ASP.NET Core packages (9.0.x → 10.0.x) and Microsoft.Identity.Web packages (3.9.4 → 3.14.1 in woodgrove-groceries to match woodgrove-groceries-api). Pin all packages to 10.0.x equivalents in Directory.Build.props (Microsoft.AspNetCore.OpenApi, Microsoft.Extensions.Caching.Memory, Microsoft.Extensions.Logging.AzureAppServices, Microsoft.AspNetCore.Authentication.JwtBearer). For graph-middleware and auth-api, also upgrade net9.0 → net10.0 and all framework packages to 10.0.x.

**Why:** .NET 9 reached EOL in May 2026; deployment on an unsupported runtime creates security gaps and is not compliant. net10.0 is LTS with 2-year support window. Version consistency (Identity.Web 3.14.1 everywhere) fixes token-handling divergence between projects. Framework-locked packages (all 9.0.9 → 10.0.x) ensure compatibility and remove deprecation warnings.

**NuGet package status:**
- woodgrove-groceries: Microsoft.Identity.Web 3.9.4 (**outdated** — 3.14.1 available); Microsoft.Graph 5.83.0 (current); ApplicationInsights 2.23.0 (current)
- woodgrove-groceries-api: Microsoft.Identity.Web 3.14.1 (current); all framework packages 9.0.9
- graph-middleware: Microsoft.Identity.Web 3.14.1 (current); Microsoft.Graph 5.94.0 (current); framework packages 9.0.9
- auth-api: framework packages 9.0.9

**Implementation roadmap:**
- Update all four .csproj files: `<TargetFramework>net10.0</TargetFramework>`
- Create or update Directory.Build.props with all 10.0.x package pins.
- Run `dotnet build` for each project; verify no compiler warnings.
- Run unit/integration tests; verify compatibility.
- Update local SDK from 9.0.x to 10.0.301 (already present: 10.0.301).
- Deploy to staging first; smoke-test token validation, Graph calls, downstream API communication.

**Decision made:** Proceed with net9.0 → net10.0 upgrade + pinned Identity.Web 3.14.1 everywhere.

---

## 2026-07-21: Security findings (consolidated)

**By:** Trinity, Tank

**What:** Five critical and six non-blocking security/correctness issues identified across auth-api, graph-middleware, and groceries-api:

**CRITICAL (action required before deployment):**

1. **Real Entra identity config committed to git (graph-middleware)** — `appsettings.Development.json` tracked with real, non-placeholder values for AzureAd:Instance, TenantId, ClientId, CertificateThumbprint (committed in dd48208, updated in 3b1e270). No plaintext secret, but thumbprint exposes app registration identity.
   - **Action:** Run `git rm --cached appsettings.Development.json`; ensure .gitignore effective; rotate certificate if sensitive; provide values via Key Vault/env vars for deployment.

2. **Most auth-api endpoints have [Authorize] commented out** — OnTokenIssuanceStartController, OnAttributeCollectionStartController, OnAttributeCollectionSubmitController, OnPageRenderStartController, SignUpStartsTestController all have `//[Authorize]`. Comments reference Easy Auth + AzureAppServiceClaimsHeader but those code paths are also commented out. Only OnOtpSendController and ActAsDemoController have active [Authorize].
   - **Action:** For demo/internal use on private App Service with network restrictions this may be acceptable, but must be consciously documented — not accidental. Document the protection model (network isolation, Easy Auth, or JWT); re-enable appropriate [Authorize] for each handler.

3. **EchoController logs raw bearer tokens (auth-api)** — `Controllers/Temporary/EchoController.cs` line 42 logs `Request.Headers.Authorization[0]` to Application Insights, emitting raw bearer tokens into telemetry storage — PII and token-leakage risk.
   - **Action:** Remove or gate behind `IsDevelopment()` check. Controller is in `Temporary/` — should not ship to production.

4. **Static HttpClient with mutable auth header — thread-safety issue (groceries-api)** — `Controllers/Demos/UserInsightsController.cs:20-44` sets `DefaultRequestHeaders.Authorization` on shared static HttpClient. Not thread-safe; concurrent requests race to set different bearer tokens on same client instance.
   - **Action:** Inject `IHttpClientFactory` and use `CreateClient()` per request, or use constructor overload with per-request auth header message handler.

5. **`new Guid()` zero-GUID invalidation bug (groceries-api)** — `Controllers/VerifyCodeController.cs:58`: `new Guid().ToString()` always produces `"00000000-0000-0000-0000-000000000000"`. After verification, code is "invalidated" with a known, predictable string; second caller who knows this could trivially pass next VerifyCode before cache expires.
   - **Action:** `Guid.NewGuid().ToString()` or better: set to `null` / remove cache entry.

**NON-BLOCKING (important but non-critical):**

- **Hardcoded app IDs should move to config** (auth-api): `99045fe1-7639-4a75-9d4a-577b6ca3810f` (Entra custom auth extension azp), partner app IDs (`7a30b8ed-...`, `65d59577-...`) hardcoded in multiple places — move to appsettings.json for per-environment overrides.
- **GraphServiceClient bypass + no token caching (graph-middleware)**: `ProfileController.cs:97` bypasses injected client and calls `MsalAccessTokenHandler.GetGraphClient()` directly per request, re-reading cert from store with no caching. Wrap in singleton/scoped service with token caching; diagnose OBO config issue first.
- **Mixed MSAL / Identity.Web token acquisition (graph-middleware)**: `MsalAccessTokenHandler` imports both Microsoft.Identity.Client and Microsoft.Identity.Web; Identity.Web should be single layer. Remove or consolidate unused `GetAccessToken` method.
- **Logging filter may suppress infrastructure logs (auth-api)**: `Program.cs:16–19` filter `provider!.ToLower().Contains("woodgroveapi")` passes only logs with "woodgroveapi" in provider name, silently dropping Microsoft.AspNetCore auth failure logs (critical for token debugging). Allow `Warning`+ from all providers.
- **`throw ex;` destroys stack trace (groceries-api)**: `Controllers/SendCodeController.cs:164` — use `throw;` to rethrow with original trace or remove try/catch entirely.
- **Verification codes stored plaintext (groceries-api)**: `SendCodeController.cs:70`, `VerifyCodeController.cs:51` have TBD comments. In-memory cache limits exposure but should be resolved before hardening.

**Decision made:** Fix all CRITICAL items before deployment. Schedule NON-BLOCKING items in next sprint. Entra identity config removal is immediate post-audit.

---

## 2026-07-21: Azure deployment topology — Windows App Service

**By:** Dozer

**What:** Deploy all four services (woodgrove-groceries, woodgrove-groceries-api, woodgrove-groceries-graph-middleware, woodgrove-auth-api) to Azure App Service on Windows (not Container Apps). Host across four App Services with single App Service Plan (P1v3). Use Key Vault for cert thumbprints and secrets. Assign system-managed identities to each App Service. Load certificates via `WEBSITE_LOAD_CERTIFICATES` so existing `MsalAccessTokenHandler.ReadCertificate()` code works unchanged. All four services require public HTTPS endpoints (no private/internal topology possible).

**Why:**
- No Dockerfiles exist; containerization adds zero functional benefit.
- web.config already present with ASP.NET Core Module V2 in-process hosting — App Service is natural deployment target.
- Windows App Service supports Windows certificate store (CurrentUser/My) used by all cert-loading services.
- App Service logging already wired (builder.Logging.AddAzureWebAppDiagnostics()) in three services.
- Custom domains + managed TLS first-class in App Service (required for woodgrovedemo.com domain enforcement in web.config).
- All four services are stateless; no database, no VNet, no Front Door, no CDN needed.
- All four services must be internet-reachable (woodgrove-auth-api called by Entra External ID cloud service; other three called by web app over internet).

**Azure resource list:**
- Resource Group: rg-woodgrove-{env}
- App Service Plan: asp-woodgrove-{env} [P1v3 Windows, single plan for all 4 apps to minimize cost]
- App Services (4): app-woodgrove-web-{env}, app-woodgrove-api-{env}, app-woodgrove-graph-{env}, app-woodgrove-auth-{env}
  - Custom domains: woodgrovedemo.com (web), api.woodgrovedemo.com (api), graph-middleware.woodgrovedemo.com (graph), auth-api.woodgrovedemo.com (auth)
  - System-assigned managed identities for each
  - WEBSITE_LOAD_CERTIFICATES: {thumbprint list or *}
- Key Vault: kv-woodgrove-{env}
  - Secrets: ApplicationInsights--ConnectionString, AzureAd--web--CertThumbprint, ArkoseFraudProtection--CertThumbprint, EmailOtp--CertThumbprint, MicrosoftGraph--CertThumbprint, GroceriesApi--Email--ConnectionString, AuthApi--Email--ConnectionString, AuthApi--CloudflareSecret, GraphMiddleware--AzureAd--CertThumbprint
  - Certificates (as certificate objects): cert-woodgrove-web-azuread, cert-woodgrove-web-arkose, cert-woodgrove-web-emailotp, cert-woodgrove-web-msgraph, cert-woodgrove-api-azuread, cert-woodgrove-graph-azuread
- Application Insights: appi-woodgrove-{env} (shared workspace for cross-service correlation)
- Log Analytics Workspace: law-woodgrove-{env}
- Azure Communication Services: acs-woodgrove-{env} (email OTP for groceries-api and auth-api)

**Per-service profiles:**

| Service | TFM | App type | Port | Config keys (sample) | KV secrets |
|---|---|---|---|---|---|
| woodgrove-groceries (web) | net9.0→10.0 | Razor Pages + MVC | 7169/5247 | AzureAd:*, WoodgroveGroceriesApi:*, GraphApi:*, MicrosoftGraph:* | 4 certs (Azure AD, Arkose, EmailOtp, Graph) + AppInsights |
| woodgrove-groceries-api | net9.0→10.0 | Web API (JWT Bearer) | 7269/5172 | AzureAd:*, AppSettings:Email:* | 1 cert (Azure AD) + ACS email connection string |
| woodgrove-groceries-graph-middleware | net9.0→10.0 | Web API (JWT Bearer) | 7283/5010 | AzureAd:*, GraphApi:* | 1 cert (Azure AD) |
| woodgrove-auth-api | net9.0→10.0 | Web API (JWT Bearer, 2 schemes) | 7086/5097 | EntraExternalIdCustomAuthToken:*, EntraExternalIdUserToken:* | ACS email + Cloudflare secret + AppInsights |

**Naming convention:** `{resourceType}-{project}-{component}-{env}` (e.g., `app-woodgrove-web-prod`, `kv-woodgrove-prod`). KV name max 24 chars: `kv-woodgrove-prod` = 17 chars ✓.

**Certificate strategy (Bicep authoring phase):** Upload certificates to Key Vault as certificate objects. Reference thumbprint as secret. Set `WEBSITE_LOAD_CERTIFICATES` on App Service to thumbprint(s) so `StoreWithThumbprint` loading continues unchanged. Each App Service's system-assigned managed identity granted Key Vault `Secrets Get` + `Certificates Get` via access policy or `Key Vault Secrets User` RBAC role.

**Inter-service call graph:** Browser → woodgrove-groceries (web) → {woodgrove-api, graph-middleware} (both public), + direct Graph via cert; woodgrove-auth-api (public) ← Entra External ID cloud service + web app (ActAsDemo endpoint); all validate tokens from Entra External ID; groceries-api + auth-api → Azure Communication Services (email OTP).

**Environment-to-domain mapping:**
| env | Web | API | Graph | Auth |
|---|---|---|---|---|
| prod | woodgrovedemo.com | api.woodgrovedemo.com | graph-middleware.woodgrovedemo.com | auth-api.woodgrovedemo.com |
| staging | staging.woodgrovedemo.com | api-staging.woodgrovedemo.com | graph-middleware-staging.woodgrovedemo.com | auth-api-staging.woodgrovedemo.com |
| dev | app-woodgrove-web-dev.azurewebsites.net | app-woodgrove-api-dev.azurewebsites.net | app-woodgrove-graph-dev.azurewebsites.net | app-woodgrove-auth-dev.azurewebsites.net |

**Decision made:** Proceed with Windows App Service topology; Bicep infrastructure-as-code authoring to follow in next phase.

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
