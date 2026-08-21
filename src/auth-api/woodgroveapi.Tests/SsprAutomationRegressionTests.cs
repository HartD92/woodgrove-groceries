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
        // The Graph API requires targetType 'allUsers' (not 'group') to actually apply
        // the policy tenant-wide; 'group' + id 'all_users' is NOT a valid group object ID
        // and would silently fail to enable Email OTP for anyone.
        Assert.Contains("targetType = 'allUsers'", workflow);
        Assert.Contains("id = 'all_users'", workflow);
        // The PATCH must actually enable the method, not merely reference it.
        Assert.Contains("state = 'enabled'", workflow);
    }

    [Fact]
    public void Workflow_MustToleratePermissionFailureWithoutFailingDeployment()
    {
        string workflow = ReadDeployWorkflow();

        // Locate the try block that immediately precedes the Email OTP PATCH call,
        // and the catch block that immediately follows it, rather than relying on a
        // fixed character offset (which would be fragile against unrelated edits and
        // could coincidentally match one of the workflow's many other try/catch blocks).
        int patchIndex = workflow.IndexOf(
            "authenticationMethodConfigurations/email", StringComparison.Ordinal);
        Assert.True(patchIndex >= 0, "Could not locate the Email OTP PATCH call.");

        int tryIndex = workflow.LastIndexOf("try {", patchIndex, StringComparison.Ordinal);
        Assert.True(tryIndex >= 0, "Could not locate a 'try {' block preceding the Email OTP PATCH call.");

        int catchIndex = workflow.IndexOf("catch {", patchIndex, StringComparison.Ordinal);
        Assert.True(catchIndex >= 0, "Could not locate a 'catch {' block following the Email OTP PATCH call.");

        // The catch block's body (up to the next line starting a new statement at the
        // same indentation, approximated here by the next blank line) must reference
        // the specific permission this PATCH needs, so failures are diagnosable.
        int catchBodyEnd = workflow.IndexOf("\r\n\r\n", catchIndex, StringComparison.Ordinal);
        if (catchBodyEnd < 0)
        {
            catchBodyEnd = workflow.Length;
        }
        string catchBody = workflow.Substring(catchIndex, catchBodyEnd - catchIndex);

        Assert.Contains("Policy.ReadWrite.AuthenticationMethod", catchBody);
    }
}
