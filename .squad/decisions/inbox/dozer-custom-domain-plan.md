### 2026-07-28: Storefront custom domain and External ID authority wire-up
**By:** Dozer
**What:** Wire the storefront to `groceries.customers.hartlabs.info` with App Service custom-hostname support, optional two-phase managed certificate enablement, and External ID authority `https://customers.hartlabs.info/`.
**Why:** WebAuthn passkey registration requires the External ID RP ID domain and storefront registrable domain to align. The hostname binding is IaC-manageable after DNS verification; the managed certificate must be enabled in a second deploy after the binding exists.
