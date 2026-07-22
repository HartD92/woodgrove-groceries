### 2026-07-22: Monorepo app deploy uses matrixed OIDC job

**By:** Dozer

**What:** Replaced the stale single-app portal-generated deployment workflow with one matrixed GitHub Actions job that publishes and deploys the web, API, auth, and graph services independently on ubuntu-latest. Each matrix leg logs into Azure through the proven azure-infra OIDC repo variables and resolves the tokenized App Service name from rg-woodgrove-dev by role infix before deployment.

**Why:** A matrix keeps the four app deployments DRY and parallel while avoiding hardcoded App Service suffix tokens or stale portal-generated secrets. Resolving the App Service name at deploy time keeps the workflow aligned with tokenized Bicep deployments and fails clearly if an expected app cannot be found.
