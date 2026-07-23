using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using woodgrovedemo.Helpers;
using woodgrovedemo.Models;

namespace woodgrovedemo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PasskeysController : ControllerBase
{
    private const string DefaultGraphApiBaseUrl = "https://graph.microsoft.com/beta";
    private readonly IConfiguration _configuration;
    private readonly TelemetryClient _telemetry;

    public PasskeysController(IConfiguration configuration, TelemetryClient telemetry)
    {
        _configuration = configuration;
        _telemetry = telemetry;
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

        using var graphResponse = await SendGraphRequestAsync(HttpMethod.Get, $"users/{userObjectId}/authentication/fido2Methods");
        string payload = await graphResponse.Content.ReadAsStringAsync();
        if (!graphResponse.IsSuccessStatusCode)
        {
            response.ErrorMessage = $"Can't read passkeys due to the following error: {payload}";
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

        string timeout = _configuration.GetSection("PasskeyManagement:ChallengeTimeoutInMinutes").Value ?? "60";
        string? userObjectId = User.GetObjectId();
        if (string.IsNullOrWhiteSpace(userObjectId))
        {
            return Ok(new PasskeyOperationResponse
            {
                ErrorMessage = "Cannot create passkey because your token doesn't contain the object identifier."
            });
        }

        using var graphResponse = await SendGraphRequestAsync(
            HttpMethod.Get,
            $"users/{userObjectId}/authentication/fido2Methods/creationOptions(challengeTimeoutInMinutes={timeout})");

        string payload = await graphResponse.Content.ReadAsStringAsync();
        if (!graphResponse.IsSuccessStatusCode)
        {
            return Ok(new PasskeyOperationResponse
            {
                ErrorMessage = $"Can't start passkey registration due to the following error: {payload}"
            });
        }

        using JsonDocument document = JsonDocument.Parse(payload);
        if (document.RootElement.TryGetProperty("publicKey", out JsonElement publicKey))
        {
            return Ok(publicKey.Clone());
        }

        return Ok(new PasskeyOperationResponse
        {
            ErrorMessage = "Passkey creation options weren't returned by Microsoft Graph."
        });
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
            return Ok(new PasskeyOperationResponse
            {
                ErrorMessage = $"Can't register passkey due to the following error: {payload}"
            });
        }

        return Ok(new PasskeyOperationResponse());
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

        using var graphResponse = await SendGraphRequestAsync(
            HttpMethod.Delete,
            $"users/{userObjectId}/authentication/fido2Methods/{Uri.EscapeDataString(id)}");

        string payload = await graphResponse.Content.ReadAsStringAsync();
        if (!graphResponse.IsSuccessStatusCode)
        {
            return Ok(new PasskeyOperationResponse
            {
                ErrorMessage = $"Can't delete passkey due to the following error: {payload}"
            });
        }

        return Ok(new PasskeyOperationResponse());
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

        string? authTime = User.Claims.FirstOrDefault(c => c.Type == "auth_time")?.Value;
        if (!string.IsNullOrWhiteSpace(authTime) &&
            long.TryParse(authTime, out long epochSeconds) &&
            DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(epochSeconds) > TimeSpan.FromMinutes(5))
        {
            errorMessage = "Passkey changes require a recent MFA challenge. Sign in again and try within 5 minutes.";
            return false;
        }

        return true;
    }

    private async Task<HttpResponseMessage> SendGraphRequestAsync(HttpMethod method, string graphPath, HttpContent? content = null)
    {
        string graphApiBaseUrl = _configuration.GetSection("PasskeyManagement:GraphApiBaseUrl").Value ?? DefaultGraphApiBaseUrl;
        string accessToken = await MsalAccessTokenHandler.AcquireToken(_configuration);

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(method, $"{graphApiBaseUrl.TrimEnd('/')}/{graphPath}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;

        return await client.SendAsync(request);
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }
}
