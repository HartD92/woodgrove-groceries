# Trinity — History

## Seed (2026-07-21)

- **Project:** woodgrove-groceries — Microsoft Entra External ID demo application.
- **My focus:** the identity/auth layer — Entra External ID flows, the custom auth extension API (`woodgrove-auth-api`), and the Graph middleware (`woodgrove-groceries-graph-middleware`).
- **Stack:** ASP.NET Core / C#, Microsoft Graph, OIDC/OAuth2.
- **Requested by:** David Hart.
- **Initial mission:** help audit the repos (identity angle), advise on monorepo consolidation, and specify the identity resources (app registrations, Key Vault secrets, managed identities) needed for Bicep deployment.

📌 Team update (2026-07-21T07:45:38Z): Completed auth-api + graph-middleware identity/security audit. Identified 5 critical findings (real Entra config in git, [Authorize] commented out, token logging, token caching, token layer consolidation) + 6 non-blocking items. Documented deployment identity config requirements. Decision merged by Scribe into shared log.
