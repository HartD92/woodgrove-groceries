# Trinity — History

## Seed (2026-07-21)

- **Project:** woodgrove-groceries — Microsoft Entra External ID demo application.
- **My focus:** the identity/auth layer — Entra External ID flows, the custom auth extension API (`woodgrove-auth-api`), and the Graph middleware (`woodgrove-groceries-graph-middleware`).
- **Stack:** ASP.NET Core / C#, Microsoft Graph, OIDC/OAuth2.
- **Requested by:** David Hart.
- **Initial mission:** help audit the repos (identity angle), advise on monorepo consolidation, and specify the identity resources (app registrations, Key Vault secrets, managed identities) needed for Bicep deployment.

📌 Team update (2026-07-21T07:45:38Z): Executed security audit fixes (removed bearer-token logging from EchoController, untracked real Entra config from graph-middleware, gated Temporary controllers via DevelopmentOnlyAttribute, standardized Graph scope to .default). 2 commits on squad/monorepo-consolidation. Build: 0 errors, 47 pre-existing C# warnings (backend only, none new).

📌 Team update (2026-07-21T11:55:23-07:00): ExtID CI/CD now uses a two-identity/two-job split. Trinity root-caused AADSTS70021 and Graph Bicep tenant targeting; Dozer implemented PR #3 at commit c5372ff with workforce ARM deployment plus ExtID Entra provisioning. — decided by David/Coordinator

