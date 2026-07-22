# Switch — History

## Seed (2026-07-21)

- **Project:** woodgrove-groceries — Microsoft Entra External ID demo application.
- **My focus:** the frontend of the main app — Razor Pages (`Pages/`, `Areas/`), `wwwroot` assets, and the presentation of each auth demo scenario.
- **Stack:** ASP.NET Core Razor Pages, HTML/CSS/JS.
- **Requested by:** David Hart.
- **Initial mission:** support the repo audit (UI/UX angle) and ensure the frontend stays clean and legible through any monorepo consolidation.

📌 Team update (2026-07-21T07:45:38Z): Executed frontend modernization: Bootstrap 4→5 migration (fixed typos, data attributes, popover JS, CSS classes, SVG xlink), WCAG accessibility (212 image alt texts, 12 spinners with loading text, keyboard controls, aria-hidden decoratives), dead assets removed (bootstrap lib, jQuery), CDN pins updated (Bootstrap 5.3.6, Icons 1.13.1, bs-stepper 1.2.0, Chart.js 4.4.9 with SRI deferred). 3 commits on squad/monorepo-consolidation. Build: 0 errors, 47 pre-existing warnings (backend only).

📌 Team update (2026-07-21T10:25:00Z): Applied PR #1 code-review fix. Corrected stale Bootstrap 5.3.3 SRI hashes to 5.3.6 (blocker — mismatched hashes block browser rendering). Completed SRI coverage (bootstrap-icons 1.13.1, bs-stepper 1.2.0, chart.js 4.4.9). Verified all hashes via exact-URL download + SHA-384 computation. Commit 2157902. Build: 0 errors. Pushed to PR #1. Ready for approval.


📌 Team update (2026-07-22T18:20:00Z): The storefront canonical-domain rewrite is removed so Azure-assigned hosts can serve the site. Runtime deployment must still supply sibling endpoints through `WoodgroveGroceriesApi__Endpoint`, `WoodgroveGroceriesAuthApi__Endpoint`, and `GraphApiMiddleware__Endpoint`; do not bake tenant IDs, subdomains, client IDs, or env-specific hostnames into committed public-repo files. CIAM sign-in authority format is `https://{subdomain}.ciamlogin.com/{tenantId}/v2.0` with `AzureAd:Domain={subdomain}.onmicrosoft.com`. — decided by David Hart
