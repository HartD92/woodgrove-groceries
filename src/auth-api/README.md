# woodgrove-auth-api

## OnPasswordSubmit just-in-time password migration

`OnPasswordSubmitController` demonstrates Microsoft Entra External ID just-in-time password migration using the shared `Woodgrove.Migration` library. The flow decrypts `data.encryptedPasswordContext`, validates the submitted password against the demo `MockLegacyIdentityProvider`, returns one of the four required `microsoft.graph.passwordsubmit.*` actions, and best-effort clears the `toBeMigrated` flag through `GraphMigrationClient` after a successful migration.

Reference:
https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-migrate-passwords-just-in-time

### Production warning: `disableStrongPassword`

Microsoft Entra External ID supports a `disableStrongPassword` option for JIT password migration scenarios where legacy passwords may not satisfy current password complexity requirements. This demo does **not** enable or automate that option. Read this carefully before using it in production: enabling it can allow migrated passwords to remain below your current External ID complexity bar until the user changes their password later.

### Registration gap: Graph beta-only custom extension

Unlike the repo's existing custom authentication extensions, the `OnPasswordSubmit` registration path is currently a Graph **beta** operation. The extension object must be created with:

`POST https://graph.microsoft.com/beta/identity/customAuthenticationExtensions`

using:

`"@odata.type": "#microsoft.graph.onPasswordSubmitCustomExtension"`

That beta-only registration is **not** automated in this PR or in `infra/main.bicep`; treat it as a manual post-deployment step.
