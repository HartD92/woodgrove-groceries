# Woodgrove.Migration.BulkImport

Demo console app for Stage 1 bulk user migration into Microsoft Entra External ID using Microsoft Graph and the `toBeMigrated` migration-flag pattern.

## Prerequisites

- A Microsoft Graph app registration configured for app-only auth with `User.ReadWrite.All` and `Application.ReadWrite.All` application permissions, with admin consent granted.
- The `b2c-extensions-app` application ID (`Migration:B2CExtensionsAppId`) from your External ID tenant.
- `Migration` settings for the extension property name and retry count.
- `MicrosoftGraph` settings for tenant ID, client ID, and either a client secret or certificate thumbprint.

## Configuration

Set values in `appsettings.json` or override with environment variables such as:

- `Migration__B2CExtensionsAppId`
- `Migration__MigrationExtensionPropertyName`
- `Migration__MaxGraphRetryAttempts`
- `MicrosoftGraph__TenantId`
- `MicrosoftGraph__ClientId`
- `MicrosoftGraph__ClientSecret`
- `MicrosoftGraph__CertificateThumbprint`

## How to run

From the repo root:

```powershell
$env:PATH = "C:\dotnet10;$env:PATH"
dotnet run --project .\src\migration\Woodgrove.Migration.BulkImport\Woodgrove.Migration.BulkImport.csproj
```

The app:

1. Ensures the `extension_{b2cExtensionsAppIdNoHyphens}_toBeMigrated` directory extension property exists.
2. Enumerates demo legacy users from `MockLegacyIdentityProvider`.
3. Generates a unique strong random temporary password per user with `RandomNumberGenerator`.
4. Creates each migrated External ID account one-by-one through Microsoft Graph.
5. Writes a JSONL report under `bin\<Configuration>\net10.0\reports\` with legacy user ID, email, Graph object ID, status, and any error.

## Throttling and large migrations

This sample processes users sequentially for demo clarity. Microsoft Graph can throttle write-heavy workloads; see https://learn.microsoft.com/en-us/graph/throttling.

For large migrations, prefer actual Graph `/$batch` request batching or a queue/worker design that can control concurrency and retries. The production-scale pattern is closer to `microsoft/b2c-to-meeid-migration-tool`, which uses worker VMs and queues rather than a single foreground console loop. That scale-out architecture is intentionally out of scope for this demo.

## Important caveat: no password-hash import

This demo does **not** import existing salted password hashes from a legacy identity store. Microsoft Graph user creation only accepts a plaintext password value for `passwordProfile.password`, so this sample sets a random temporary password plus the `toBeMigrated` flag.

Real credential migration must happen later through JIT password migration (#71) or a legacy-IdP-driven credential harvesting flow. Do not treat this sample as password-hash import support, because Graph has no capability to upload or inject a legacy bcrypt/PBKDF2/scrypt hash directly into an External ID user.