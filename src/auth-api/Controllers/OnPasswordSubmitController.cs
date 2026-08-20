using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Woodgrove.Migration.Abstractions;
using Woodgrove.Migration.Contracts;
using Woodgrove.Migration.Graph;
using Woodgrove.Migration.Options;
using Woodgrove.Migration.Security;
using woodgroveapi.Helpers;

namespace woodgroveapi.Controllers;

/// <summary>
/// Handles Microsoft Entra External ID OnPasswordSubmit callbacks for demo just-in-time password migration.
/// Reference: https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-migrate-passwords-just-in-time
/// </summary>
[Authorize(AuthenticationSchemes = "EntraExternalIdCustomAuthToken")]
[ApiController]
[Route("[controller]")]
public class OnPasswordSubmitController : ControllerBase
{
    private readonly ILogger<OnPasswordSubmitController> _logger;
    private readonly TelemetryClient _telemetry;
    private readonly ILegacyIdentityProvider _legacyIdentityProvider;
    private readonly MigrationOptions _migrationOptions;
    private readonly JweDecryptor _jweDecryptor;
    private readonly Func<string, CancellationToken, Task> _markUserMigratedAsync;

    public OnPasswordSubmitController(
        ILogger<OnPasswordSubmitController> logger,
        TelemetryClient telemetry,
        ILegacyIdentityProvider legacyIdentityProvider,
        GraphMigrationClient graphMigrationClient,
        IOptions<MigrationOptions> migrationOptions,
        JweDecryptor jweDecryptor)
        : this(
            logger,
            telemetry,
            legacyIdentityProvider,
            migrationOptions,
            jweDecryptor,
            graphMigrationClient.MarkUserMigratedAsync)
    {
    }

    internal OnPasswordSubmitController(
        ILogger<OnPasswordSubmitController> logger,
        TelemetryClient telemetry,
        ILegacyIdentityProvider legacyIdentityProvider,
        IOptions<MigrationOptions> migrationOptions,
        JweDecryptor jweDecryptor,
        Func<string, CancellationToken, Task> markUserMigratedAsync)
    {
        _logger = logger;
        _telemetry = telemetry;
        _legacyIdentityProvider = legacyIdentityProvider;
        _migrationOptions = migrationOptions.Value;
        _jweDecryptor = jweDecryptor;
        _markUserMigratedAsync = markUserMigratedAsync;
    }

    [HttpPost(Name = "OnPasswordSubmit")]
    public async Task<OnPasswordSubmitResponse> PostAsync([FromBody] OnPasswordSubmitRequest requestPayload, CancellationToken cancellationToken)
    {
        var telemetry = new PageViewTelemetry("OnPasswordSubmit");
        telemetry.Properties.Add("TenantId", requestPayload.data.tenantId);
        telemetry.Properties.Add("CorrelationId", requestPayload.data.authenticationContext.correlationId);
        telemetry.Properties.Add("EventListenerId", requestPayload.data.authenticationEventListenerId);
        telemetry.Properties.Add("AuthenticationExtensionId", requestPayload.data.customAuthenticationExtensionId);
        telemetry.Properties.Add("Protocol", requestPayload.data.authenticationContext.protocol ?? string.Empty);
        telemetry.Properties.Add("AppDisplayName", requestPayload.data.authenticationContext.clientServicePrincipal?.appDisplayName ?? string.Empty);
        telemetry.Properties.Add("AppId", requestPayload.data.authenticationContext.clientServicePrincipal?.appId ?? string.Empty);
        _telemetry.TrackPageView(telemetry);

        var certificate = MigrationCertificateLoader.Load(_migrationOptions);
        var passwordContext = _jweDecryptor.Decrypt(requestPayload.data.encryptedPasswordContext, certificate);
        var legacyResult = await _legacyIdentityProvider.ValidateAsync(passwordContext.Username, passwordContext.UserPassword, cancellationToken);

        var response = await BuildResponseAsync(legacyResult, passwordContext.Nonce, requestPayload.data.authenticationContext.user.id, cancellationToken);

        _logger.LogInformation(
            "OnPasswordSubmit completed with action {Action} for correlation {CorrelationId}, userId {UserId}, username {Username}",
            response.data.actions[0].odatatype,
            requestPayload.data.authenticationContext.correlationId,
            requestPayload.data.authenticationContext.user.id,
            passwordContext.Username);

        return response;
    }

    private async Task<OnPasswordSubmitResponse> BuildResponseAsync(
        LegacyValidationResult legacyResult,
        string nonce,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (legacyResult.UserFound && legacyResult.PasswordValid && !legacyResult.RequiresPasswordUpdate)
        {
            await TryMarkUserMigratedAsync(userId, cancellationToken);
            return OnPasswordSubmitResponseBuilder.MigratePassword(nonce);
        }

        if (legacyResult.UserFound && legacyResult.PasswordValid && legacyResult.RequiresPasswordUpdate)
        {
            return OnPasswordSubmitResponseBuilder.UpdatePassword(
                nonce,
                "Password update required",
                "Your legacy password was accepted, but you need to choose a stronger password before continuing.");
        }

        if (legacyResult.UserFound && !legacyResult.PasswordValid && !legacyResult.IsBlocked)
        {
            return OnPasswordSubmitResponseBuilder.Retry(
                nonce,
                "Incorrect password",
                "The password you entered didn't match our legacy account records. Try again.");
        }

        return OnPasswordSubmitResponseBuilder.Block(
            nonce,
            "Sign-in unavailable",
            legacyResult.IsBlocked
                ? legacyResult.BlockReason ?? "This legacy account is locked or disabled."
                : "We couldn't find a matching legacy account for this sign-in.");
    }

    private async Task TryMarkUserMigratedAsync(string? userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Skipping migration flag update because the request did not include a user id.");
            return;
        }

        try
        {
            await _markUserMigratedAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph migration flag update failed for user {UserId}. Returning MigratePassword anyway.", userId);
        }
    }
}
