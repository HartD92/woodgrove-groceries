using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;

namespace woodgrovedemo.Tests;

public class AuthRedirectCustomizerTests
{
    [Fact]
    public void Apply_UsesDefaultCustomDomain_WithoutLosingSignupPrompt()
    {
        var protocolMessage = new OpenIdConnectMessage
        {
            IssuerAddress = "https://hlacustomer.ciamlogin.com/tenant/oauth2/v2.0/authorize"
        };

        AuthRedirectCustomizer.Apply(
            protocolMessage,
            new Dictionary<string, string?>
            {
                ["prompt"] = "create"
            },
            defaultDomain: "customers.hartlabs.info");

        Assert.Equal("create", protocolMessage.Prompt);
        Assert.Equal("customers.hartlabs.info", new Uri(protocolMessage.IssuerAddress).Host);
    }

    [Fact]
    public void Apply_PreservesExistingIssuerQuery_WhenRewritingHost()
    {
        var protocolMessage = new OpenIdConnectMessage
        {
            IssuerAddress = "https://hlacustomer.ciamlogin.com/tenant/oauth2/v2.0/authorize?existing=1"
        };

        AuthRedirectCustomizer.Apply(
            protocolMessage,
            new Dictionary<string, string?>(),
            defaultDomain: "customers.hartlabs.info");

        var issuerAddress = new Uri(protocolMessage.IssuerAddress);

        Assert.Equal("customers.hartlabs.info", issuerAddress.Host);
        Assert.Equal("?existing=1", issuerAddress.Query);
    }

    [Fact]
    public void Apply_AddsCustomQueryStringParameters()
    {
        var protocolMessage = new OpenIdConnectMessage
        {
            IssuerAddress = "https://hlacustomer.ciamlogin.com/tenant/oauth2/v2.0/authorize"
        };

        AuthRedirectCustomizer.Apply(
            protocolMessage,
            new Dictionary<string, string?>
            {
                ["query-string"] = "enablewaf=true&r=tor"
            });

        Assert.Equal("true", protocolMessage.Parameters["enablewaf"]);
        Assert.Equal("tor", protocolMessage.Parameters["r"]);
    }

    [Fact]
    public void Apply_RewritesLogoutHost_WithoutChangingPostLogoutRedirectUri()
    {
        var protocolMessage = new OpenIdConnectMessage
        {
            IssuerAddress = "https://hlacustomer.ciamlogin.com/tenant/oauth2/v2.0/logout",
            PostLogoutRedirectUri = "https://groceries.customers.hartlabs.info/signout-callback-oidc"
        };

        AuthRedirectCustomizer.Apply(
            protocolMessage,
            new Dictionary<string, string?>(),
            defaultDomain: "customers.hartlabs.info");

        Assert.Equal("customers.hartlabs.info", new Uri(protocolMessage.IssuerAddress).Host);
        Assert.Equal("https://groceries.customers.hartlabs.info/signout-callback-oidc", protocolMessage.PostLogoutRedirectUri);
    }
}
