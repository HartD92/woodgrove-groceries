using System.Text.RegularExpressions;
using Xunit;

namespace woodgroveapi.Tests;

/// <summary>
/// Regression tests for the infra/main.bicep wiring of the two JWT bearer schemes used
/// by src/auth-api (EntraExternalIdCustomAuthToken and EntraExternalIdUserToken).
///
/// Background (AADSTS1100001 / underlying error 1003002 passkey sign-in bug):
/// - EntraExternalIdCustomAuthToken validates the bearer token sent by Microsoft's
///   first-party "Azure AD Authentication Extensions" app (appId
///   99045fe1-7639-4a75-9d4a-577b6ca3810f) when it invokes our custom authentication
///   extension callbacks (OnTokenIssuanceStart, OnAttributeCollectionStart/Submit,
///   onPageRenderStart). That token is issued from login.microsoftonline.com.
/// - EntraExternalIdUserToken validates bearer tokens issued to real end users
///   (e.g. ActAsDemoController), which ARE issued from the tenant's CIAM origin host
///   (ciamlogin.com).
///
/// These two schemes must never be pointed at the same metadata address/issuer host,
/// or one of the two callers will fail JWT issuer/signing-key validation and get
/// rejected with 401 -- which Entra surfaces to end users as AADSTS1100001 during
/// sign-in (including passkey sign-in, since OnTokenIssuanceStart fires on every
/// sign-in).
/// </summary>
public class InfraTokenMetadataRegressionTests
{
    private static string ReadMainBicep()
    {
        // Walk up from the test assembly's output directory to the repo root, then
        // down into infra/main.bicep. This avoids depending on the test runner's
        // working directory.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "infra", "main.bicep")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "infra", "main.bicep"));
    }

    [Fact]
    public void CustomAuthToken_And_UserToken_MetadataAddresses_MustNotShareTheSameVariable()
    {
        string bicep = ReadMainBicep();

        var customAuthMatch = Regex.Match(bicep,
            @"\{\s*name:\s*'EntraExternalIdCustomAuthToken__MetadataAddress',\s*value:\s*(\w+)\s*\}");
        var userTokenMatch = Regex.Match(bicep,
            @"\{\s*name:\s*'EntraExternalIdUserToken__MetadataAddress',\s*value:\s*(\w+)\s*\}");

        Assert.True(customAuthMatch.Success, "Could not find EntraExternalIdCustomAuthToken__MetadataAddress app setting in infra/main.bicep.");
        Assert.True(userTokenMatch.Success, "Could not find EntraExternalIdUserToken__MetadataAddress app setting in infra/main.bicep.");

        string customAuthVariable = customAuthMatch.Groups[1].Value;
        string userTokenVariable = userTokenMatch.Groups[1].Value;

        Assert.NotEqual(userTokenVariable, customAuthVariable);
    }

    [Fact]
    public void CustomAuthToken_MetadataAddress_MustResolveToLoginMicrosoftOnline()
    {
        string bicep = ReadMainBicep();

        var customAuthMatch = Regex.Match(bicep,
            @"\{\s*name:\s*'EntraExternalIdCustomAuthToken__MetadataAddress',\s*value:\s*(\w+)\s*\}");
        Assert.True(customAuthMatch.Success, "Could not find EntraExternalIdCustomAuthToken__MetadataAddress app setting in infra/main.bicep.");

        string variableName = customAuthMatch.Groups[1].Value;

        var variableDeclMatch = Regex.Match(bicep,
            $@"var\s+{Regex.Escape(variableName)}\s*=\s*'([^']*)'");
        Assert.True(variableDeclMatch.Success, $"Could not find declaration of variable '{variableName}' in infra/main.bicep.");

        string variableValue = variableDeclMatch.Groups[1].Value;

        Assert.Contains("login.microsoftonline.com", variableValue);
        Assert.DoesNotContain("ciamlogin.com", variableValue);
    }

    [Fact]
    public void UserToken_MetadataAddress_MustResolveToCiamOriginHost()
    {
        string bicep = ReadMainBicep();

        var userTokenMatch = Regex.Match(bicep,
            @"\{\s*name:\s*'EntraExternalIdUserToken__MetadataAddress',\s*value:\s*(\w+)\s*\}");
        Assert.True(userTokenMatch.Success, "Could not find EntraExternalIdUserToken__MetadataAddress app setting in infra/main.bicep.");

        string variableName = userTokenMatch.Groups[1].Value;

        var variableDeclMatch = Regex.Match(bicep,
            $@"var\s+{Regex.Escape(variableName)}\s*=\s*'([^']*)'");
        Assert.True(variableDeclMatch.Success, $"Could not find declaration of variable '{variableName}' in infra/main.bicep.");

        string variableValue = variableDeclMatch.Groups[1].Value;

        // The user token scheme validates real end-user sign-in tokens, which are
        // issued from the tenant's CIAM origin host (entraOriginHost), not from
        // login.microsoftonline.com.
        Assert.Contains("entraOriginHost", variableValue);
    }
}
