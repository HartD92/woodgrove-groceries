# Decision: Wire UserRoles — Provision App Roles + Security Groups, Inject IDs via IaC

**Author:** Trinity (Auth/Security specialist)  
**Date:** 2026-07-22  
**Status:** Implemented in PR — pending merge

---

## Context

The `/profile` page "Your Woodgrove app roles and security groups" demo (`UserRolesController`) reads
five `AppRoles:*` configuration keys. Until this change, those keys were all-zeros placeholders in
`src/storefront/appsettings.json` and the controller's `Guid.Parse` calls would fail at runtime.

---

## Decision

### Two App Roles provisioned on the WEB app registration (`woodgrove-groceries (<env>)`)

| Config key | App role `value` | Notes |
|---|---|---|
| `AppRoles:OrdersManager` | `Orders.Manager` | `allowedMemberTypes: ["User"]`, displayName "Orders Manager" |
| `AppRoles:ProductsContributor` | `Products.Contributor` | `allowedMemberTypes: ["User"]`, displayName "Products Contributor" |

**IDs are provisioned dynamically** — the entra-provision job uses `New-StableGuid` (deterministic
MD5-based UUID from `[$envName, 'woodgrove-web', $roleValue]`) if the role doesn't yet exist, or
reuses the existing `id` from the app's `appRoles` collection if it does.  The array is always
read-merge-patched (never clobbered) to protect any pre-existing roles and to avoid orphaning user
assignments.

### Two Security Groups created in the ExtID tenant

| Config key | `displayName` | `mailNickname` | Notes |
|---|---|---|---|
| `AppRoles:CommercialAccountsSecurityGroup` | `Woodgrove Commercial Accounts` | `woodgroveCommercialAccounts` | `securityEnabled: true`, `mailEnabled: false` |
| `AppRoles:ExclusiveDemosSecurityGroup` | `Woodgrove Exclusive Demos` | `woodgroveExclusiveDemos` | `securityEnabled: true`, `mailEnabled: false` |

Groups are created idempotently — filtered by `mailNickname` via Graph API; existing objectId is
reused if found.

### `AppRoles:PrincipalId`

Set to `$webSp.id` — the **service principal objectId** of the web app registration in the ExtID
tenant.  The controller uses this as `ResourceId` in `AppRoleAssignment` POST calls.  It is sourced
from the already-resolved `$webSp` variable (no new lookup needed).

### Five App Settings injected into the storefront web app

Bicep parameter → App Service setting (double-underscore convention):

| Bicep param | App setting key | Source |
|---|---|---|
| `appRolesPrincipalId` | `AppRoles__PrincipalId` | `$webSp.id` from entra-provision job |
| `appRolesOrdersManager` | `AppRoles__OrdersManager` | `Orders.Manager` appRole id |
| `appRolesProductsContributor` | `AppRoles__ProductsContributor` | `Products.Contributor` appRole id |
| `appRolesCommercialGroup` | `AppRoles__CommercialAccountsSecurityGroup` | Commercial Accounts group objectId |
| `appRolesExclusiveDemosGroup` | `AppRoles__ExclusiveDemosSecurityGroup` | Exclusive Demos group objectId |

### `appsettings.json` placeholders are intentional

`src/storefront/appsettings.json` retains `"00000000-0000-0000-00000000000000000"` placeholders for
all five AppRoles keys.  This is by design: the file is committed to a public repository and **must
never contain real tenant/app/group/role GUIDs**.  Real values flow from IaC at deploy time as Azure
App Service application settings, which override `appsettings.json` at runtime via the ASP.NET
configuration hierarchy.

### No controller changes

`UserRolesController.cs` is unchanged.  It already reads the five `AppRoles:*` keys from
`IConfiguration`; the fix is entirely in provisioning the Entra objects and injecting their IDs.

### No new Graph permissions needed

The web app SP already has `AppRoleAssignment.ReadWrite.All` and `Group.ReadWrite.All` granted by
the entra-provision job (lines ~582-588 of the workflow).  Verified — no additions required.

---

## Files changed

- `.github/workflows/deploy-infra.yml` — entra-provision job: added `Ensure-SecurityGroup` helper,
  app roles idempotent provisioning, security group creation, 5 new job outputs; azure-deploy job:
  5 new env vars + 5 bicep parameters passed in what-if and deploy commands.
- `infra/main.bicep` — 5 new parameters; 5 new app settings entries in `webApp` module call.
- `infra/main.json` — regenerated from `az bicep build`.
