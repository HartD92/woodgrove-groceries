using Azure.Communication.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;
using System.Security.Cryptography;
using System.Text;

namespace woodgrove_groceries_api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class VerifyCodeController : ControllerBase
{
    private readonly ILogger<VerifyCodeController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;

    public VerifyCodeController(ILogger<VerifyCodeController> logger, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _configuration = configuration;
        _memoryCache = memoryCache;
    }

    [HttpPost(Name = "VerifyCode")]
    public Task<VerifyCodeResponse> OnPostAsync([FromBody] VerifyCodeRequest request)
    {
        // Check the user object ID
        if (User == null || User.GetObjectId() == null)
        {
            return Task.FromResult(new VerifyCodeResponse("Error: User object ID is null"));
        }

        string userID = User.GetObjectId()!;
        VerifyCodeResponse response = new VerifyCodeResponse();
        response.ValidationPassed = false;

        // Try to get the cache object for the current user
        if (_memoryCache.TryGetValue(userID, out AuthMethod? cachedAuthMethod) && cachedAuthMethod is not null)
        {
            // Increase the number of user tries
            cachedAuthMethod.Validations++;

            if (IsAboveThreshold(cachedAuthMethod))
            {
                _memoryCache.Remove(userID);
                Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return Task.FromResult(new VerifyCodeResponse("You have reached the maximum number of allowed verifications. Request a new code and try again."));
            }

            if (VerificationCodeMatches(request.VerificationCode, cachedAuthMethod.VerificationCode))
            {
                response.ValidationPassed = true;
                response.AuthType = cachedAuthMethod.AuthType;
                response.AuthValue = cachedAuthMethod.AuthValue;

                _memoryCache.Remove(userID);
            }
            else
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(1));
                _memoryCache.Set(userID, cachedAuthMethod, cacheEntryOptions);
            }
        }

        return Task.FromResult(response);
    }

    private bool IsAboveThreshold(AuthMethod authMethod)
    {
        // Get app settings
        int maxRetry = _configuration.GetValue<int>("AppSettings:MaxRetry", 3);

        // Check if the user's validation in the last hour reached the threshold
        return authMethod.Validations > maxRetry;
    }

    internal static bool VerificationCodeMatches(string providedCode, string storedHashedCode)
    {
        if (string.IsNullOrWhiteSpace(providedCode) || string.IsNullOrWhiteSpace(storedHashedCode))
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedCode));
        var storedHash = Convert.FromHexString(storedHashedCode);
        return CryptographicOperations.FixedTimeEquals(providedHash, storedHash);
    }
}
