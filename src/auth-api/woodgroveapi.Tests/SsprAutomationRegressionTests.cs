using Xunit;

namespace woodgroveapi.Tests;

/// <summary>
/// Regression tests for the SSPR (self-service password reset) automation added to
/// .github/workflows/deploy-infra.yml (see issue #79). The `entra-provision` job
/// enables the Email OTP authentication method tenant-wide via a Microsoft Graph PATCH
/// to policies/authenticationMethodsPolicy/authenticationMethodConfigurations/email --
/// a prerequisite for the SSPR "Forgot password?" flow described in
/// src/storefront/Areas/Help/Pages/SSPR.cshtml.
///
/// These tests parse the workflow file directly (similar to
/// InfraTokenMetadataRegressionTests parsing infra/main.bicep) to lock in that this
/// automation isn't silently removed or misconfigured in a future edit.
/// </summary>
public class SsprAutomationRegressionTests
{
    private static string ReadDeployWorkflow()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, ".github", "workflows", "deploy-infra.yml")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, ".github", "workflows", "deploy-infra.yml"));
    }

    [Fact]
    public void Workflow_MustPatchEmailAuthenticationMethodConfiguration()
    {
        string workflow = ReadDeployWorkflow();

        Assert.Contains(
            "https://graph.microsoft.com/v1.0/policies/authenticationMethodsPolicy/authenticationMethodConfigurations/email",
            workflow);
    }

    [Fact]
    public void Workflow_MustEnableEmailOtpForAllUsers()
    {
        string workflow = ReadDeployWorkflow();

        Assert.Contains("allowExternalIdToUseEmailOtp", workflow);
        Assert.Contains("all_users", workflow);
        // The PATCH must actually enable the method, not merely reference it.
        Assert.Contains("state = 'enabled'", workflow);
    }

    [Fact]
    public void Workflow_MustToleratePermissionFailureWithoutFailingDeployment()
    {
        string workflow = ReadDeployWorkflow();

        // The Email OTP PATCH must be wrapped so a deployer identity missing
        // Policy.ReadWrite.AuthenticationMethod logs a warning rather than aborting
        // the whole deployment (this permission is optional/documented as manual
        // fallback in infra/README.md).
        int patchIndex = workflow.IndexOf(
            "authenticationMethodConfigurations/email", StringComparison.Ordinal);
        Assert.True(patchIndex >= 0, "Could not locate the Email OTP PATCH call.");

        string surroundingText = workflow.Substring(Math.Max(0, patchIndex - 400),
            Math.Min(1200, workflow.Length - Math.Max(0, patchIndex - 400)));

        Assert.Contains("try", surroundingText);
        Assert.Contains("catch", surroundingText);
        Assert.Contains("Policy.ReadWrite.AuthenticationMethod", surroundingText);
    }
}
