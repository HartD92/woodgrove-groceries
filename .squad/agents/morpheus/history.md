# Morpheus — History

## Seed (2026-07-21)

- **Project:** woodgrove-groceries — a Microsoft Entra External ID demo application.
- **This repo:** ASP.NET Core Razor Pages app (C#) — the customer-facing storefront demonstrating Entra External ID sign-in/sign-up flows.
- **Related components:**
  - `woodgrove-groceries-api` — the Woodgrove Groceries web API
  - `woodgrove-groceries-graph-middleware` — middleware for Microsoft Graph
  - `woodgrove-auth-api` — custom authentication extension web API for Entra External ID
- **Requested by:** David Hart
- **Initial mission:** (1) audit all four repos for needed updates, (2) evaluate consolidating into a monorepo, (3) author Bicep to deploy the whole system to Azure.
- **My role:** own the monorepo strategy and cross-repo architecture decisions.

📌 Team update (2026-07-21T07:45:38Z): Executed 4-repo monorepo consolidation Phase 1. Relocated storefront, imported 3 repos via git subtree (full history preserved), created root woodgrove.slnx. Build: 0 errors, 36 pre-existing warnings. 6 commits on squad/monorepo-consolidation, awaiting PR review.
