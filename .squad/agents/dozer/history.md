# Dozer — History

## Seed (2026-07-21)

- **Project:** woodgrove-groceries — Microsoft Entra External ID demo application.
- **My focus:** Azure infrastructure and deployment — authoring Bicep to deploy the whole system (main app + 3 APIs/middleware), Key Vault, managed identity, and CI/CD.
- **Stack:** Azure, Bicep, GitHub Actions; components are ASP.NET Core / C#.
- **Requested by:** David Hart.
- **Initial mission:** author Bicep to deploy everything to Azure, and advise on how a monorepo layout should map to deployable units.

📌 Team update (2026-07-21T07:45:38Z): Completed Azure topology survey + infrastructure design. Recommended Windows App Service (not Container Apps): documented per-service profiles, inter-service call graph, full Azure resource list (ASP, 4x App Services, Key Vault, AppInsights, Log Analytics, ACS), naming convention, certificate strategy. Ready for Bicep IaC authoring. Decision merged by Scribe into shared log.

📌 Team update (2026-07-21T09:30:00Z): Completed Entra IaC authoring. Extended infra/ with bicepconfig.json (MS Graph v1.0 extension), entraApps.bicep (4 app registrations), deploy-infra.yml (OIDC passwordless workflow). Identified critical passwordCredentials blocker by Fact Checker verification + patched (63564d2): removed Bicep passwordCredentials block, automated web secret via `az ad app credential reset` in workflow. Both commits on squad/monorepo-consolidation, not pushed.

📌 Team update (2026-07-21T10:35:00Z): Applied PR #1 code-review fix. Made deploy-infra.yml web-client-secret provisioning idempotent: skip on subsequent pushes (secret already in KV), create only on first provision, rotate only via explicit workflow_dispatch input (prunes expired credentials defensively). Closes final PR #1 code-review item. Commit e3d8a6b, pushed to PR #1. All PR #1 review items resolved.

📌 Team update (2026-07-21T11:55:23-07:00): ExtID CI/CD now uses a two-identity/two-job split. Trinity root-caused AADSTS70021 and Graph Bicep tenant targeting; Dozer implemented PR #3 at commit c5372ff with workforce ARM deployment plus ExtID Entra provisioning. — decided by David/Coordinator
📌 Team update (2026-07-21T16:52:42-07:00): Two-tenant CIAM deploy pipeline is green after PRs #8-#13. Durable infra lessons: MCAP internal subscriptions can gate App Service compute quota by SKU/region (westus2 worked where eastus2 did not); use exact Key Vault built-in role GUIDs; RBAC-enabled KV must grant the deploy service principal Secrets Officer via `deployer().objectId`; GitHub pwsh can fail on stale `$LASTEXITCODE`; this subscription needs `SecurityControl: Ignore` tags to keep public network access enabled for CI secret writes.


📌 Team update (2026-07-22T18:20:00Z): Post-deploy fixes merged to main. CIAM authority must be built as `https://{subdomain}.ciamlogin.com/{tenantId}/v2.0` with `AzureAd:Domain={subdomain}.onmicrosoft.com`; current deploy variables resolve subdomain `hlacustomer` and tenant ID `1a845386-636a-4d10-a25b-9ece94a1302d`. Public repo directive: keep tenant IDs, subdomains, client IDs, and env-specific values out of committed app/workflow/infra parameter files; source them from GitHub repo variables/secrets at deploy time. Also avoid manual `workflow_dispatch` immediately after merging `.github/workflows/deploy-infra.yml` changes because the push trigger already starts a deploy and concurrent runs can collide with `DeploymentActive`. — decided by David Hart

📌 Team update (2026-07-22T18:49:00Z): CIAM correction: Microsoft.Identity.Web + Entra External ID must use Authority-only subdomain-root `https://{subdomain}.ciamlogin.com/` with no `TenantId`, `Domain`, or `Instance`; workforce-style `/{tenantId}/v2.0` caused `IDW10503` during `/signin-oidc` token redemption. This supersedes the earlier workforce-shaped authority note. — decided by David Hart/Trinity
