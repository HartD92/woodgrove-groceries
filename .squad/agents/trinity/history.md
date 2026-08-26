# Trinity — History

## Seed (2026-07-21)

- **Project:** woodgrove-groceries — Microsoft Entra External ID demo application.
- **My focus:** the identity/auth layer — Entra External ID flows, the custom auth extension API (`woodgrove-auth-api`), and the Graph middleware (`woodgrove-groceries-graph-middleware`).
- **Stack:** ASP.NET Core / C#, Microsoft Graph, OIDC/OAuth2.
- **Requested by:** David Hart.
- **Initial mission:** help audit the repos (identity angle), advise on monorepo consolidation, and specify the identity resources (app registrations, Key Vault secrets, managed identities) needed for Bicep deployment.

📌 Team update (2026-07-21T07:45:38Z): Executed security audit fixes (removed bearer-token logging from EchoController, untracked real Entra config from graph-middleware, gated Temporary controllers via DevelopmentOnlyAttribute, standardized Graph scope to .default). 2 commits on squad/monorepo-consolidation. Build: 0 errors, 47 pre-existing C# warnings (backend only, none new).

📌 Team update (2026-07-21T11:55:23-07:00): ExtID CI/CD now uses a two-identity/two-job split. Trinity root-caused AADSTS70021 and Graph Bicep tenant targeting; Dozer implemented PR #3 at commit c5372ff with workforce ARM deployment plus ExtID Entra provisioning. — decided by David/Coordinator
📌 Team update (2026-07-21T16:52:42-07:00): Deploy pipeline auth/RBAC lesson: the CI deploy service principal needs Key Vault Secrets Officer on RBAC-enabled Key Vaults, assigned with Bicep `deployer().objectId`, so workflow secret read/write steps can seed `web-client-secret`.

📌 Team update (2026-07-22T18:49:00Z): CIAM correction: Microsoft.Identity.Web + Entra External ID must use Authority-only subdomain-root `https://{subdomain}.ciamlogin.com/` with no `TenantId`, `Domain`, or `Instance`; workforce-style `/{tenantId}/v2.0` caused `IDW10503` during `/signin-oidc` token redemption. — decided by David Hart/Trinity

📌 Team update (2026-08-26T12:17:28.998-07:00): Researched issue #83 login branding. Recommendation: use External ID Company Branding + branded custom URL domain as the primary fix; avoid assuming Azure AD B2C-style hosted HTML/JS templates exist in External ID. Repo already has reusable branding assets, Front Door custom-domain plumbing, and an `onPageRenderStart` branding override for optional polish.

📌 Team update (2026-08-26T12:34:19-07:00): Implemented the Abercrombie & Fitch demo branding pack for issue #83. Added sourced logo assets (af-logo.svg + light derivative), upload-safe PNG/JPG companions, infra/scripts/Apply-CompanyBranding.ps1 for Graph-based Company Branding updates, and updated onPageRenderStart to emit the new colors, logos, square logos, background, favicon, and CSS. Validation: branding asset dimensions verified; repo build could not run locally because only .NET SDK 9.0.317 is installed while the projects target net10.0.

📌 Team update (2026-08-26T12:45:25-07:00): Public-repo branding correction for issue #83: removed all committed A&F image/logo files from the branch, switched login/email branding to BrandAssets__BaseUrl / BRAND_ASSETS_BASE_URL, kept only CSS/text fallback in-repo, and updated the Graph branding script/docs to expect approved assets in the private blob container.

📌 Team update (2026-08-26T12:58:55-07:00): Addressed PR #87 branding review. Fixed Apply-CompanyBranding.ps1 to send the real Graph bearer token and a valid Accept-Language: en-US header, re-verified PR #85 branch squad/84-afd-signup-authority-fix now exposes blob-level anonymous read (llowBlobPublicAccess: true, publicAccess: 'Blob'), and updated docs/comments to reflect that direct brand-asset URLs are intentionally supported while container listing stays disabled.
