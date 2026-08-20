using Xunit;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace woodgrovedemo.Tests;

public class AuthSchemeSelectorTests
{
    [Fact]
    public void Select_ReturnsDefaultScheme_WhenNoOverridesExist()
    {
        var scheme = AuthSchemeSelector.Select(handler: null, cookieKeys: []);

        Assert.Equal(OpenIdConnectDefaults.AuthenticationScheme, scheme);
    }

    [Theory]
    [InlineData(".AspNetCore.ArkoseFraudProtectionCookies", AuthScheme.ArkoseFraudProtection)]
    [InlineData(".AspNetCore.EmailOtpCookies", AuthScheme.EmailOtp)]
    public void Select_UsesCookieScheme_WhenMatchingCookieExists(string cookieKey, string expectedScheme)
    {
        var scheme = AuthSchemeSelector.Select(handler: null, cookieKeys: [cookieKey]);

        Assert.Equal(expectedScheme, scheme);
    }

    [Theory]
    [InlineData(AuthScheme.ArkoseFraudProtection)]
    [InlineData(AuthScheme.EmailOtp)]
    public void Select_HandlerOverride_WinsOverCookieSelection(string handler)
    {
        var scheme = AuthSchemeSelector.Select(
            handler,
            cookieKeys: [".AspNetCore.ArkoseFraudProtectionCookies"]);

        Assert.Equal(handler, scheme);
    }
}
