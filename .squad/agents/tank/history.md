# Tank — History

## Seed (2026-07-21)

- **Project:** woodgrove-groceries — Microsoft Entra External ID demo application.
- **My focus:** backend APIs — primarily `woodgrove-groceries-api`, plus backend logic in the main app and .NET/NuGet version currency across all components.
- **Stack:** ASP.NET Core / C#.
- **Requested by:** David Hart.
- **Initial mission:** audit the API repos for needed updates (framework versions, dependencies, deprecated patterns), support monorepo consolidation, and ensure APIs are deployable via the planned Bicep.

📌 Team update (2026-07-21T07:45:38Z): Executed .NET 10 LTS upgrade (all 4 projects net9.0→10.0), package convergence (Identity.Web 3.14.1, Graph 5.94.0), backend P0/P1 bug fixes (zero-GUID, stack trace, [HttpGet] routes, BuildServiceProvider, HttpClient thread-safety, dead code cleanup). 3 commits on squad/monorepo-consolidation. Build: 0 errors, 12 NuGet warnings (transitive pre-existing CVEs).

📌 Team update (2026-07-21T10:25:00Z): Applied PR #1 code-review fix. Removed unused Microsoft.AspNetCore.OpenApi from src/api + src/auth-api (eliminates high-severity GHSA-v5pm-xwqc-g5wc transitive CVE). Commit b0579c2. Build: 0 errors. Pushed to PR #1. Ready for approval.
