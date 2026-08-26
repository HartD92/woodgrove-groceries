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
public class SendCodeController : ControllerBase
{
    private readonly ILogger<SendCodeController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private const int VerificationCodeDigits = 6;

    public SendCodeController(ILogger<SendCodeController> logger, IConfiguration configuration, IMemoryCache memoryCache)
    {
        _logger = logger;
        _configuration = configuration;
        _memoryCache = memoryCache;
    }


    [HttpPost(Name = "SendCode")]
    public async Task<SendCodeResponse> OnPostAsync([FromBody] SendCodeRequest request)
    {
        // Check the user object ID
        if (User == null || User.GetObjectId() == null)
        {
            return new SendCodeResponse("Error: User object ID is null");
        }

        string userID = User.GetObjectId()!;
        AuthMethod? authMethod = null;

        // Try to get the cache object for the current user
        if (_memoryCache.TryGetValue(userID, out AuthMethod? cachedAuthMethod))
        {
            // Get the value from the cache
            authMethod = cachedAuthMethod;
        }

        // If the cache is null
        if (authMethod == null)
        {
            // Init a new one
            authMethod = new AuthMethod
            {
                UID = userID
            };
        }

        // Set the values
        authMethod.AuthType = request.AuthType;
        authMethod.AuthValue = request.AuthValue;
        authMethod.MessagesSent++;
        // Reset the validations to zero
        authMethod.Validations = 0;

        // Check if the user's validation in the last hour reached the threshold
        if (IsAboveThreshold(authMethod))
        {
            return new SendCodeResponse("You have reached the number of verification code you can send. Please wait an hour and try again.");
        }

        var verificationCode = GenerateVerificationCode();
        authMethod.VerificationCode = HashVerificationCode(verificationCode);

        // Save data in cache
        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromHours(1));
        _memoryCache.Set(userID, authMethod, cacheEntryOptions);

        if (authMethod.AuthType == AuthMethodType.EmailMfa || authMethod.AuthType == AuthMethodType.SignInEmail)
        {
            try
            {
                await SendEmailAsync(authMethod, verificationCode);
            }
            catch (System.Exception ex)
            {
                return new SendCodeResponse(ex.Message);
            }

        }

        return new SendCodeResponse();
    }

    private bool IsAboveThreshold(AuthMethod authMethod)
    {
        // Get app settings
        int userThreshold = _configuration.GetValue<int>("AppSettings:UserThreshold", 3);

        // Check if the user's validation in the last hour reached the threshold
        return authMethod.MessagesSent > userThreshold;
    }

    private static string GenerateVerificationCode()
    {
        var maxValue = (int)Math.Pow(10, VerificationCodeDigits);
        var code = RandomNumberGenerator.GetInt32(0, maxValue);
        return code.ToString($"D{VerificationCodeDigits}");
    }

    internal static string HashVerificationCode(string verificationCode)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(verificationCode));
        return Convert.ToHexString(hash);
    }

    private async Task SendEmailAsync(AuthMethod authMethod, string? plaintextCode = null)
    {
        string emailConnectionString = _configuration.GetSection("AppSettings:Email:ConnectionString").Value!;
        string emailSender = _configuration.GetSection("AppSettings:Email:Sender").Value!;
        var codeForEmail = plaintextCode ?? string.Empty;

        try
        {
            var emailClient = new EmailClient(emailConnectionString);

            var subject = "Your Woodgrove account verification code";
            var brandedLogoMarkup = GetBrandLogoMarkup();
            var htmlContent = @$"<html><body>
            <div style='background-color: #1F6402!important; padding: 15px'>
                <table>
                <tbody>
                    <tr>
                        <td colspan='2' style='padding: 0px;font-family: &quot;Segoe UI Semibold&quot;, &quot;Segoe UI Bold&quot;, &quot;Segoe UI&quot;, &quot;Helvetica Neue Medium&quot;, Arial, sans-serif;font-size: 17px;color: white;'>Woodgrove Groceries live demo</td>
                    </tr>
                    <tr>
                        <td colspan='2' style='padding: 15px 0px 0px;font-family: &quot;Segoe UI Light&quot;, &quot;Segoe UI&quot;, &quot;Helvetica Neue Medium&quot;, Arial, sans-serif;font-size: 35px;color: white;'>Your Woodgrove verification code</td>
                    </tr>
                    <tr>
                        <td colspan='2' style='padding: 25px 0px 0px;font-family: &quot;Segoe UI&quot;, Tahoma, Verdana, Arial, sans-serif;font-size: 14px;color: white;'> To access <span style='font-family: &quot;Segoe UI Bold&quot;, &quot;Segoe UI Semibold&quot;, &quot;Segoe UI&quot;, &quot;Helvetica Neue Medium&quot;, Arial, sans-serif; font-size: 14px; font-weight: bold; color: white;'>Woodgrove Groceries</span>'s app, please copy and enter the code below into the sign-up or sign-in page. This code is valid for 30 minutes. </td>
                    </tr>
                    <tr>
                        <td colspan='2' style='padding: 25px 0px 0px;font-family: &quot;Segoe UI&quot;, Tahoma, Verdana, Arial, sans-serif;font-size: 14px;color: white;'>Your account verification code:</td>
                    </tr>
                    <tr>
                        <td style='padding: 0px;font-family: &quot;Segoe UI Bold&quot;, &quot;Segoe UI Semibold&quot;, &quot;Segoe UI&quot;, &quot;Helvetica Neue Medium&quot;, Arial, sans-serif;font-size: 25px;font-weight: bold;color: white;padding-top: 5px;'>
                        {codeForEmail}</td>
                        <td rowspan='3' style='text-align: center;'>
                            <img src='https://woodgrovedemo.com/custom-email/shopping.png' style='border-radius: 50%; width: 100px'>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding: 25px 0px 0px;font-family: &quot;Segoe UI&quot;, Tahoma, Verdana, Arial, sans-serif;font-size: 14px;color: white;'> If you didn't request a code, you can ignore this email. </td>
                    </tr>
                    <tr>
                        <td style='padding: 25px 0px 0px;font-family: &quot;Segoe UI&quot;, Tahoma, Verdana, Arial, sans-serif;font-size: 14px;color: white;'> Best regards, </td>
                    </tr>
                    <tr>
                        <td>
                            {brandedLogoMarkup}
                        </td>
                        <td style='font-family: &quot;Segoe UI&quot;, Tahoma, Verdana, Arial, sans-serif;font-size: 14px;color: white; text-align: center;'>
                            <a href='https://woodgrovedemo.com/Privacy' style='color: white; text-decoration: none;'>Privacy Statement</a>
                        </td>
                    </tr>
                </tbody>
                </table>
            </div>
            </body></html>";


            EmailSendOperation emailSendOperation = await emailClient.SendAsync(
                Azure.WaitUntil.Started,
                emailSender,
                authMethod.AuthValue,
                subject,
                htmlContent);

        }
        catch (System.Exception)
        {
            throw;
        }
    }

    private string GetBrandLogoMarkup()
    {
        var brandAssetsBaseUrl = _configuration["BrandAssets:BaseUrl"];

        if (string.IsNullOrWhiteSpace(brandAssetsBaseUrl))
        {
            brandAssetsBaseUrl = _configuration["BRAND_ASSETS_BASE_URL"];
        }

        if (!string.IsNullOrWhiteSpace(brandAssetsBaseUrl))
        {
            return $"<img src='{brandAssetsBaseUrl.Trim().TrimEnd('/')}/af-headerlogo.png' height='20' alt='Brand logo'>";
        }

        return "<span style='font-family: Georgia, serif; letter-spacing: 0.2em; font-size: 12px; color: white;'>ABERCROMBIE &amp; FITCH</span>";
    }
}
