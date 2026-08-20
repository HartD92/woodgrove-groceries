using Microsoft.AspNetCore.Authentication.OpenIdConnect;

public static class AuthSchemeSelector
{
    public static string Select(string? handler, IEnumerable<string> cookieKeys)
    {
        string scheme = OpenIdConnectDefaults.AuthenticationScheme;

        if (cookieKeys.Contains(".AspNetCore.ArkoseFraudProtectionCookies"))
        {
            scheme = AuthScheme.ArkoseFraudProtection;
        }
        else if (cookieKeys.Contains(".AspNetCore.EmailOtpCookies"))
        {
            scheme = AuthScheme.EmailOtp;
        }

        if (handler == AuthScheme.ArkoseFraudProtection || handler == AuthScheme.EmailOtp)
        {
            scheme = handler;
        }

        return scheme;
    }
}
