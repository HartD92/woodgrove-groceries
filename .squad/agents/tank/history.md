# Tank — History

## Seed (2026-07-21)

- **Project:** woodgrove-groceries — Microsoft Entra External ID demo application.
- **My focus:** backend APIs — primarily `woodgrove-groceries-api`, plus backend logic in the main app and .NET/NuGet version currency across all components.
- **Stack:** ASP.NET Core / C#.
- **Requested by:** David Hart.
- **Initial mission:** audit the API repos for needed updates (framework versions, dependencies, deprecated patterns), support monorepo consolidation, and ensure APIs are deployable via the planned Bicep.

📌 Team update (2026-07-21T07:45:38Z): Completed backend audit (groceries-api + main app backend layer). Catalogued 28 findings: 3 P0 breaking (net9.0 EOL, missing [HttpGet] attrs, zero-GUID bug), 5 P1 (thread-safety, stack trace, version mismatch), 5 P2 (code quality), 10 P3 (hygiene). Prioritized fix list provided. Decision merged by Scribe into shared log.
