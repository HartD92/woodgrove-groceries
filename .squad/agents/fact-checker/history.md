# Fact Checker — History

## Seed (2026-07-21)

- **Project:** woodgrove-groceries — Microsoft Entra External ID demo application.
- **My focus:** Verification of hallucination-prone claims in code, IaC, and architecture docs. Fact-check against live sources (Git repos, APIs, official docs, live registries).
- **Stack:** Bicep, Azure APIs, GitHub, Entra/MS Graph.
- **Requested by:** David Hart.
- **Initial mission:** verify Dozer's Entra IaC + OIDC workflow against live sources; flag any inconsistencies or deployment blockers.

📌 Team update (2026-07-21T09:30:00Z): Completed verification of Dozer Entra IaC (5 items: MS Graph Bicep extension tag, delegated scope GUIDs, application permission GUIDs, customAuthenticationExtensions availability, passwordCredentials block). Identified **critical deployment blocker**: passwordCredentials cannot be created via Bicep (Graph API requires `addPassword` service action; PUT/PATCH fails). Dozer patched (63564d2): removed block, automated web secret via `az ad app credential reset` → KV in workflow. All other items verified correct. Decision merged by Scribe into shared log.
