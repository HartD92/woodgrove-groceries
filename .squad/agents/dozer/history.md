# Dozer — History

## Seed (2026-07-21)

- **Project:** woodgrove-groceries — Microsoft Entra External ID demo application.
- **My focus:** Azure infrastructure and deployment — authoring Bicep to deploy the whole system (main app + 3 APIs/middleware), Key Vault, managed identity, and CI/CD.
- **Stack:** Azure, Bicep, GitHub Actions; components are ASP.NET Core / C#.
- **Requested by:** David Hart.
- **Initial mission:** author Bicep to deploy everything to Azure, and advise on how a monorepo layout should map to deployable units.

📌 Team update (2026-07-21T07:45:38Z): Completed Azure topology survey + infrastructure design. Recommended Windows App Service (not Container Apps): documented per-service profiles, inter-service call graph, full Azure resource list (ASP, 4x App Services, Key Vault, AppInsights, Log Analytics, ACS), naming convention, certificate strategy. Ready for Bicep IaC authoring. Decision merged by Scribe into shared log.
