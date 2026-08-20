using System.Text.Json;
using Woodgrove.Migration.Contracts;
using Xunit;

namespace Woodgrove.Migration.Tests;

public class OnPasswordSubmitContractsTests
{
    public static TheoryData<OnPasswordSubmitResponse, string, string?> Responses => new()
    {
        { OnPasswordSubmitResponseBuilder.MigratePassword("nonce-1"), OnPasswordSubmitActionTypes.MigratePassword, null },
        { OnPasswordSubmitResponseBuilder.UpdatePassword("nonce-2", "Reset required", "Choose a stronger password."), OnPasswordSubmitActionTypes.UpdatePassword, "Choose a stronger password." },
        { OnPasswordSubmitResponseBuilder.Retry("nonce-3", "Try again", "Password was incorrect."), OnPasswordSubmitActionTypes.Retry, "Password was incorrect." },
        { OnPasswordSubmitResponseBuilder.Block("nonce-4", "Blocked", "Legacy account is locked."), OnPasswordSubmitActionTypes.Block, "Legacy account is locked." }
    };

    [Theory]
    [MemberData(nameof(Responses))]
    public void Builder_SerializesExpectedActionType(OnPasswordSubmitResponse response, string expectedOdataType, string? expectedMessage)
    {
        var json = JsonSerializer.Serialize(response);
        if (expectedMessage is not null)
        {
            Assert.Contains(expectedMessage, json, StringComparison.Ordinal);
        }
        using var document = JsonDocument.Parse(json);

        var data = document.RootElement.GetProperty("data");
        Assert.Equal("microsoft.graph.onPasswordSubmitResponseData", data.GetProperty("@odata.type").GetString());
        Assert.NotNull(data.GetProperty("nonce").GetString());

        var action = data.GetProperty("actions")[0];
        Assert.Equal(expectedOdataType, action.GetProperty("@odata.type").GetString());
        if (expectedMessage is null)
        {
            Assert.False(action.TryGetProperty("message", out _));
        }
        else
        {
            Assert.True(action.TryGetProperty("message", out _) || action.TryGetProperty("Message", out _));
        }
    }
}
