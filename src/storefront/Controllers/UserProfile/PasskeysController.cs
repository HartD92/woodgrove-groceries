using System.Text;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;
using woodgrovedemo.Helpers;
using woodgrovedemo.Models;

namespace woodgrovedemo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PasskeysController : ControllerBase
{
    // NOTE: FIDO2 provisioning APIs are in Microsoft Graph beta as of 2026-07 and may change without deprecation notice.
    private const string DefaultGraphApiBaseUrl = "https://graph.microsoft.com/beta";
    private static readonly string[] GraphDelegatedScopes = ["https://graph.microsoft.com/UserAuthenticationMethod.ReadWrite.All"];
    private readonly IConfiguration _configuration;
    private readonly TelemetryClient _telemetry;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthorizationHeaderProvider _authorizationHeaderProvider;

    public PasskeysController(
        IConfiguration configuration,
        TelemetryClient telemetry,
        IHttpClientFactory httpClientFactory,
        IAuthorizationHeaderProvider authorizationHeaderProvider)
    {
        _configuration = configuration;
        _telemetry = telemetry;
        _httpClientFactory = httpClientFactory;
        _authorizationHeaderProvider = authorizationHeaderProvider;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        _telemetry.TrackPageView("Profile:Passkeys:List");
        var response = new PasskeyListResponse();

        string? userObjectId = User.GetObjectId();
        if (string.IsNullOrWhiteSpace(userObjectId))
        {
            response.ErrorMessage = "Cannot read passkeys because your token doesn't contain the object identifier.";
            return Ok(response);
        }

        try
        {
            using var graphResponse = await SendGraphRequestAsync(HttpMethod.Get, $"users/{userObjectId}/authentication/fido2Methods");
            string payload = await graphResponse.Content.ReadAsStringAsync();
            if (!graphResponse.IsSuccessStatusCode)
            {
                _telemetry.TrackTrace($"[Passkeys:List] Graph error: {payload}");
                string safeMsg = TryExtractGraphErrorMessage(payload) ?? "An unexpected error occurred.";
                response.ErrorMessage = $"Can't read passkeys: {safeMsg}";
                return Ok(response);
            }

            using JsonDocument document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("value", out var values) && values.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in values.EnumerateArray())
                {
                    response.Passkeys.Add(new PasskeyInfo
                    {
                        Id = ReadString(item, "id"),
                        DisplayName = ReadString(item, "displayName"),
                        Model = ReadString(item, "model"),
                        PasskeyType = ReadString(item, "passkeyType"),
                        CreatedDateTime = ReadString(item, "createdDateTime"),
                        LastUsedDateTime = ReadString(item, "lastUsedDateTime")
                    });
                }
            }
        }
        catch (Exception ex)
        {
            AppInsights.TrackException(_telemetry, ex, "Passkeys:List");
            response.ErrorMessage = ex is MicrosoftIdentityWebChallengeUserException
                ? GetGraphAuthorizationChallengeMessage(ex)
                : "Can't read passkeys right now. Please try again.";
        }

        return Ok(response);
    }

    [HttpGet("creation-options")]
    public async Task<IActionResult> GetCreationOptionsAsync()
    {
        _telemetry.TrackPageView("Profile:Passkeys:CreationOptions");
        if (!IsMfaChallengeFresh(out var errorMessage))
        {
            return Ok(new PasskeyOperationResponse { ErrorMessage = errorMessage! });
        }

        string? userObjectId = User.GetObjectId();
        if (string.IsNullOrWhiteSpace(userObjectId))
        {
            return Ok(new PasskeyOperationResponse
            {
                ErrorMessage = "Cannot create passkey because your token doesn't contain the object identifier."
            });
        }

        try
        {
            // Validate timeout config: must be a positive integer; fall back to 60 if missing or malformed.
            string rawTimeout = _configuration.GetSection("PasskeyManagement:ChallengeTimeoutInMinutes").Value ?? "60";
            string timeout = int.TryParse(rawTimeout, out int t) && t > 0 ? t.ToString() : "60";

            using var graphResponse = await SendGraphRequestAsync(
                HttpMethod.Get,
                $"users/{userObjectId}/authentication/fido2Methods/creationOptions(challengeTimeoutInMinutes={timeout})");

            string payload = await graphResponse.Content.ReadAsStringAsync();
            if (!graphResponse.IsSuccessStatusCode)
            {
                _telemetry.TrackTrace($"[Passkeys:CreationOptions] Graph error: {payload}");
                string safeMsg = TryExtractGraphErrorMessage(payload) ?? "An unexpected error occurred.";
                return Ok(new PasskeyOperationResponse
                {
                    ErrorMessage = $"Can't start passkey registration: {safeMsg}"
                });
            }

            using JsonDocument document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("publicKey", out JsonElement publicKey))
            {
                // Intentionally returns the raw WebAuthn publicKey options object on success so the client
                // can pass it directly to navigator.credentials.create(). On error this endpoint returns a
                // PasskeyOperationResponse with errorMessage instead. The client distinguishes the two shapes
                // by the presence of errorMessage. Do not wrap in a typed envelope without a coordinated
                // frontend change.
                return Ok(publicKey.Clone());
            }

            return Ok(new PasskeyOperationResponse
            {
                ErrorMessage = "Passkey creation options weren't returned by Microsoft Graph."
            });
        }
        catch (Exception ex)
        {
            AppInsights.TrackException(_telemetry, ex, "Passkeys:CreationOptions");
            return Ok(new PasskeyOperationResponse
            {
                ErrorMessage = ex is MicrosoftIdentityWebChallengeUserException
                    ? GetGraphAuthorizationChallengeMessage(ex)
                    : "Can't start passkey registration right now. Please try again."
            });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync([FromBody] PasskeyRegisterRequest request)
    {
        _telemetry.TrackPageView("Profile:Passkeys:Register");
        if (!IsMfaChallengeFresh(out var errorMessage))
        {
            return Ok(new PasskeyOperationResponse { ErrorMessage = errorMessage! });
        }

        string? userObjectId = User.GetObjectId();
        if (string.IsNullOrWhiteSpace(userObjectId))
        {
            return Ok(new PasskeyOperationResponse
            {
                ErrorMessage = "Cannot register passkey because your token doesn't contain the object identifier."
            });
        }

        try
        {
            string displayNamePrefix = _configuration.GetSection("PasskeyManagement:DisplayNamePrefix").Value ?? "passkey";
            string displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? $"{displayNamePrefix}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"
                : request.DisplayName;

            var payloadJson = JsonSerializer.Serialize(new
            {
                publicKeyCredential = request.PublicKeyCredential,
                displayName
            });

            using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
            using var graphResponse = await SendGraphRequestAsync(
                HttpMethod.Post,
                $"users/{userObjectId}/authentication/fido2Methods",
                content);

            string payload = await graphResponse.Content.ReadAsStringAsync();
            if (!graphResponse.IsSuccessStatusCode)
            {
                _telemetry.TrackTrace($"[Passkeys:Register] Graph error: {payload}");
                string safeMsg = TryExtractGraphErrorMessage(payload) ?? "An unexpected error occurred.";
                return Ok(new PasskeyOperationResponse
                {
                    ErrorMessage = $"Can't register passkey: {safeMsg}"
                });
            }

            return Ok(new PasskeyOperationResponse());
        }
        catch (Exception ex)
        {
            AppInsights.TrackException(_telemetry, ex, "Passkeys:Register");
            return Ok(new PasskeyOperationResponse
            {
                ErrorMessage = ex is MicrosoftIdentityWebChallengeUserException
                    ? GetGraphAuthorizationChallengeMessage(ex)
                    : "Can't register passkey right now. Please try again."
            });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id)
    {
        _telemetry.TrackPageView("Profile:Passkeys:Delete");
        if (!IsMfaChallengeFresh(out var errorMessage))
        {
            return Ok(new PasskeyOperationResponse { ErrorMessage = errorMessage! });
        }

        string? userObjectId = User.GetObjectId();
        if (string.IsNullOrWhiteSpace(userObjectId))
        {
            return Ok(new PasskeyOperationResponse
            {
                ErrorMessage = "Cannot delete passkey because your token doesn't contain the object identifier."
            });
        }

        try
        {
            using var graphResponse = await SendGraphRequestAsync(
                HttpMethod.Delete,
                $"users/{userObjectId}/authentication/fido2Methods/{Uri.EscapeDataString(id)}");

            string payload = await graphResponse.Content.ReadAsStringAsync();
            if (!graphResponse.IsSuccessStatusCode)
            {
                _telemetry.TrackTrace($"[Passkeys:Delete] Graph error: {payload}");
                string safeMsg = TryExtractGraphErrorMessage(payload) ?? "An unexpected error occurred.";
                return Ok(new PasskeyOperationResponse
                {
                    ErrorMessage = $"Can't delete passkey: {safeMsg}"
                });
            }

            return Ok(new PasskeyOperationResponse());
        }
        catch (Exception ex)
        {
            AppInsights.TrackException(_telemetry, ex, "Passkeys:Delete");
            return Ok(new PasskeyOperationResponse
            {
                ErrorMessage = ex is MicrosoftIdentityWebChallengeUserException
                    ? GetGraphAuthorizationChallengeMessage(ex)
                    : "Can't delete passkey right now. Please try again."
            });
        }
    }

    private bool IsMfaChallengeFresh(out string? errorMessage)
    {
        errorMessage = null;
        bool mfaFulfilled = User.Claims.Any(c => c.Type == "acrs" && c.Value == "c1");
        if (!mfaFulfilled)
        {
            errorMessage = "Multi-factor authentication is required for this operation.";
            return false;
        }

        const string freshnessMessage = "Passkey changes require a recent MFA challenge. Sign in again and try within 5 minutes.";

        string? authTime = User.Claims.FirstOrDefault(c => c.Type == "auth_time")?.Value;
        if (string.IsNullOrWhiteSpace(authTime))
        {
            _telemetry.TrackTrace("[Passkeys] MFA freshness gate denied: auth_time claim absent — verify tenant policy emits this claim");
            errorMessage = freshnessMessage;
            return false;
        }

        if (!long.TryParse(authTime, out long epochSeconds) ||
            epochSeconds < DateTimeOffset.MinValue.ToUnixTimeSeconds() ||
            epochSeconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            _telemetry.TrackTrace("[Passkeys] MFA freshness gate denied: auth_time claim present but unparseable or out of range");
            errorMessage = freshnessMessage;
            return false;
        }

        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(epochSeconds);

        // Allow up to 2 minutes of future clock skew between this server and the IDP.
        // NTP-synced cloud hosts rarely drift beyond 1 second; 2 minutes gives a generous
        // safety margin without materially weakening the 5-minute freshness window.
        if (age < TimeSpan.FromMinutes(-2))
        {
            _telemetry.TrackTrace("[Passkeys] MFA freshness gate denied: auth_time future-dated beyond 2-minute clock-skew tolerance");
            errorMessage = freshnessMessage;
            return false;
        }

        if (age > TimeSpan.FromMinutes(5))
        {
            _telemetry.TrackTrace("[Passkeys] MFA freshness gate denied: auth_time stale");
            errorMessage = freshnessMessage;
            return false;
        }

        return true;
    }

    private async Task<HttpResponseMessage> SendGraphRequestAsync(HttpMethod method, string graphPath, HttpContent? content = null)
    {
        string graphApiBaseUrl = _configuration.GetSection("PasskeyManagement:GraphApiBaseUrl").Value ?? DefaultGraphApiBaseUrl;
        string authorizationHeader = await _authorizationHeaderProvider.CreateAuthorizationHeaderForUserAsync(GraphDelegatedScopes);

        // IHttpClientFactory manages connection pooling; do not dispose the client.
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(method, $"{graphApiBaseUrl.TrimEnd('/')}/{graphPath}");
        request.Headers.Add("Authorization", authorizationHeader);
        request.Content = content;

        return await client.SendAsync(request);
    }

    private static string GetGraphAuthorizationChallengeMessage(Exception ex)
    {
        if (ex is MicrosoftIdentityWebChallengeUserException challengeException &&
            challengeException.MsalUiRequiredException.ErrorCode == "user_null")
        {
            return "The token cache does not contain the token to access Microsoft Graph. Sign out and sign in again to authorize passkey management.";
        }

        return "Sign in again to authorize passkey management.";
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string? TryExtractGraphErrorMessage(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("error", out var errorObj) &&
                errorObj.TryGetProperty("message", out var msg) &&
                msg.ValueKind == JsonValueKind.String)
            {
                return msg.GetString();
            }
        }
        catch (JsonException)
        {
            // Payload is not valid JSON; fall through to return null.
        }
        return null;
    }
}
