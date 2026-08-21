using Xunit;

namespace woodgroveapi.Tests;

/// <summary>
/// Regression tests for the SMS authentication method automation added to
/// .github/workflows/deploy-infra.yml (see issue #80, "Option A"). The
/// `entra-provision` job creates/reuses a dedicated "Woodgrove SMS Authentication"
/// security group, then enables the SMS authentication method for that group via a
/// Microsoft Graph PATCH to
/// policies/authenticationMethodsPolicy/authenticationMethodConfigurations/sms -- a
/// prerequisite described in src/storefront/Areas/Help/Pages/SmsAuthentication.cshtml.
///
/// Unlike Email OTP (see SsprAutomationRegressionTests), the SMS authentication
/// method's includeTargets does NOT support a synthetic "allUsers" target -- Graph
/// only accepts a real group object ID, so this automation must target the dedicated
/// group rather than 'allUsers'.
///
/// These tests parse the workflow file directly (same approach as
/// SsprAutomationRegressionTests) to lock in that this automation isn't silently
/// removed or misconfigured in a future edit.
/// </summary>
public class SmsAuthenticationAutomationRegressionTests
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
    public void Workflow_MustPatchSmsAuthenticationMethodConfiguration()
    {
        string workflow = ReadDeployWorkflow();

        Assert.Contains(
            "https://graph.microsoft.com/v1.0/policies/authenticationMethodsPolicy/authenticationMethodConfigurations/sms",
            workflow);
    }

    [Fact]
    public void Workflow_MustCreateDedicatedGroupAndTargetItNotAllUsers()
    {
        string workflow = ReadDeployWorkflow();

        // SMS's includeTargets does not support a synthetic 'allUsers' target the way
        // Email OTP's does -- Graph requires a real group object ID. If a future edit
        // "simplifies" this to mirror the Email OTP 'allUsers' pattern, the PATCH will
        // silently fail to enable SMS for anyone.
        Assert.Contains("Ensure-SecurityGroup -DisplayName 'Woodgrove SMS Authentication'", workflow);
        Assert.Contains("targetType = 'group'", workflow);
        Assert.Contains("id = $smsAuthGroupId", workflow);

        // The include target passed to the SMS PATCH must be the dedicated group's
        // targetType/id pair, not the 'allUsers' synthetic target Email OTP uses --
        // verify by locating the include-target block immediately following the SMS
        // PATCH URI and asserting it does not contain 'allUsers'.
        int patchIndex = workflow.IndexOf(
            "authenticationMethodConfigurations/sms", StringComparison.Ordinal);
        Assert.True(patchIndex >= 0, "Could not locate the SMS authentication PATCH call.");
        int bodyEnd = workflow.IndexOf("})", patchIndex, StringComparison.Ordinal);
        Assert.True(bodyEnd >= 0, "Could not locate the end of the SMS PATCH body.");
        string patchBody = workflow.Substring(patchIndex, bodyEnd - patchIndex);
        Assert.DoesNotContain("allUsers", patchBody);
        Assert.DoesNotContain("all_users", patchBody);

        // The PATCH must actually enable the method, not merely reference it.
        Assert.Contains("state = 'enabled'", workflow);
    }

    [Fact]
    public void Workflow_MustToleratePermissionOrBillingFailureWithoutFailingDeployment()
    {
        string workflow = ReadDeployWorkflow();

        // Locate the try block that immediately precedes the SMS PATCH call, and the
        // catch block that immediately follows it, rather than relying on a fixed
        // character offset (fragile against unrelated edits and could coincidentally
        // match one of the workflow's many other try/catch blocks).
        int patchIndex = workflow.IndexOf(
            "authenticationMethodConfigurations/sms", StringComparison.Ordinal);
        Assert.True(patchIndex >= 0, "Could not locate the SMS authentication PATCH call.");

        int tryIndex = workflow.LastIndexOf("try {", patchIndex, StringComparison.Ordinal);
        Assert.True(tryIndex >= 0, "Could not locate a 'try {' block preceding the SMS PATCH call.");

        int catchIndex = workflow.IndexOf("catch {", patchIndex, StringComparison.Ordinal);
        Assert.True(catchIndex >= 0, "Could not locate a 'catch {' block following the SMS PATCH call.");

        // The catch block's body (up to the next blank line, approximating the end of
        // the statement) must reference both non-automatable prerequisites -- the
        // Graph permission and the Azure billing link -- so failures are diagnosable.
        int catchBodyEnd = workflow.IndexOf("\r\n\r\n", catchIndex, StringComparison.Ordinal);
        if (catchBodyEnd < 0)
        {
            catchBodyEnd = workflow.Length;
        }
        string catchBody = workflow.Substring(catchIndex, catchBodyEnd - catchIndex);

        Assert.Contains("Policy.ReadWrite.AuthenticationMethod", catchBody);
        Assert.Contains("Azure subscription", catchBody);
    }
}
