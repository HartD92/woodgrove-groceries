# Woodgrove.Migration

Shared migration primitives for the Woodgrove Entra External ID demo. This library exists so the future bulk import app (#70) and future JIT password-migration controller (#71) can share the same legacy-provider seam, Graph helper, JWE decryptor, and strongly typed `onPasswordSubmit` contracts.

## ILegacyIdentityProvider

`ILegacyIdentityProvider` is the abstraction between Woodgrove and any legacy identity store. Bulk import will enumerate users through `EnumerateUsersAsync`, while the JIT flow will validate a submitted password through `ValidateAsync`.

`MockLegacyIdentityProvider` is a demo-only stand-in for a real LDAP, SQL, or legacy IdP integration. It is intentionally seeded with a few predictable users so the sample can demonstrate all four `onPasswordSubmit` outcomes. It must not be used as a production identity provider.

## JIT contract shapes

The library models the Microsoft Entra External ID `onPasswordSubmit` request and response payloads, including the encrypted password context claims (`user-password`, `username`, `nonce`) and the four response actions:

- `microsoft.graph.passwordsubmit.MigratePassword`
- `microsoft.graph.passwordsubmit.UpdatePassword`
- `microsoft.graph.passwordsubmit.Retry`
- `microsoft.graph.passwordsubmit.Block`

Reference:
https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-migrate-passwords-just-in-time

## Graph usage

`GraphMigrationClient` wraps Microsoft Graph user creation and migration-flag updates. It mirrors the repo's existing Graph app-only auth pattern by reading tenant/client settings plus either `MicrosoftGraph:ClientSecret` or a certificate thumbprint from `IConfiguration`.

## Certificate loading

For JIT decryption, callers can load the private certificate from plain configuration values that were themselves populated by App Service Key Vault references. The helper supports either a cert thumbprint (certificate store lookup) or a base64 PKCS#12 value and optional password from the `Migration` config section. The library does not make direct Key Vault SDK calls.

## Planned consumers

- #70 will use this library for bulk enumeration plus Graph user creation.
- #71 will use this library for `onPasswordSubmit` request binding, JWE decryption, legacy validation, and response construction.
