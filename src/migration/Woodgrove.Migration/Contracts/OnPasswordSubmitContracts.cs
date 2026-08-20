using System.Text.Json.Serialization;

namespace Woodgrove.Migration.Contracts;

public sealed class OnPasswordSubmitRequest
{
    public string type { get; set; } = "microsoft.graph.authenticationEvent.passwordSubmit";
    public string source { get; set; } = string.Empty;
    public OnPasswordSubmitRequestData data { get; set; } = new();
}

public sealed class OnPasswordSubmitRequestData
{
    [JsonPropertyName("@odata.type")]
    public string odatatype { get; set; } = "microsoft.graph.onPasswordSubmitCalloutData";

    public string tenantId { get; set; } = string.Empty;
    public string authenticationEventListenerId { get; set; } = string.Empty;
    public string customAuthenticationExtensionId { get; set; } = string.Empty;
    public string encryptedPasswordContext { get; set; } = string.Empty;
    public OnPasswordSubmitAuthenticationContext authenticationContext { get; set; } = new();
}

public sealed class OnPasswordSubmitAuthenticationContext
{
    public string correlationId { get; set; } = string.Empty;
    public OnPasswordSubmitClientContext client { get; set; } = new();
    public string? protocol { get; set; }
    public OnPasswordSubmitServicePrincipal? clientServicePrincipal { get; set; }
    public OnPasswordSubmitServicePrincipal? resourceServicePrincipal { get; set; }
    public OnPasswordSubmitUserContext user { get; set; } = new();
}

public sealed class OnPasswordSubmitClientContext
{
    public string? ip { get; set; }
    public string? locale { get; set; }
    public string? market { get; set; }
}

public sealed class OnPasswordSubmitServicePrincipal
{
    public string? id { get; set; }
    public string? appId { get; set; }
    public string? appDisplayName { get; set; }
    public string? displayName { get; set; }
}

public sealed class OnPasswordSubmitUserContext
{
    public string? id { get; set; }
    public string? userPrincipalName { get; set; }
    public string? mail { get; set; }
    public string? displayName { get; set; }
    public string? givenName { get; set; }
    public string? surname { get; set; }
}

public sealed class OnPasswordSubmitResponse
{
    public OnPasswordSubmitResponseData data { get; set; } = new();
}

public sealed class OnPasswordSubmitResponseData
{
    [JsonPropertyName("@odata.type")]
    public string odatatype { get; set; } = "microsoft.graph.onPasswordSubmitResponseData";

    public List<OnPasswordSubmitAction> actions { get; set; } = [];

    public string nonce { get; set; } = string.Empty;
}

 [JsonDerivedType(typeof(MigratePasswordAction))]
 [JsonDerivedType(typeof(UpdatePasswordAction))]
 [JsonDerivedType(typeof(RetryAction))]
 [JsonDerivedType(typeof(BlockAction))]
public abstract class OnPasswordSubmitAction
{
    [JsonPropertyName("@odata.type")]
    public string odatatype { get; protected init; } = string.Empty;
}

public sealed class MigratePasswordAction : OnPasswordSubmitAction
{
    public MigratePasswordAction() => odatatype = OnPasswordSubmitActionTypes.MigratePassword;
}

public sealed class UpdatePasswordAction : OnPasswordSubmitAction
{
    public UpdatePasswordAction() => odatatype = OnPasswordSubmitActionTypes.UpdatePassword;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? title { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? message { get; set; }
}

public sealed class RetryAction : OnPasswordSubmitAction
{
    public RetryAction() => odatatype = OnPasswordSubmitActionTypes.Retry;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? title { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? message { get; set; }
}

public sealed class BlockAction : OnPasswordSubmitAction
{
    public BlockAction() => odatatype = OnPasswordSubmitActionTypes.Block;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? title { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? message { get; set; }
}

public static class OnPasswordSubmitActionTypes
{
    public const string MigratePassword = "microsoft.graph.passwordsubmit.MigratePassword";
    public const string UpdatePassword = "microsoft.graph.passwordsubmit.UpdatePassword";
    public const string Retry = "microsoft.graph.passwordsubmit.Retry";
    public const string Block = "microsoft.graph.passwordsubmit.Block";
}

public static class OnPasswordSubmitResponseBuilder
{
    public static OnPasswordSubmitResponse MigratePassword(string nonce) => Create(nonce, new MigratePasswordAction());

    public static OnPasswordSubmitResponse UpdatePassword(string nonce, string? title = null, string? message = null) =>
        Create(nonce, new UpdatePasswordAction { title = title, message = message });

    public static OnPasswordSubmitResponse Retry(string nonce, string? title = null, string? message = null) =>
        Create(nonce, new RetryAction { title = title, message = message });

    public static OnPasswordSubmitResponse Block(string nonce, string? title = null, string? message = null) =>
        Create(nonce, new BlockAction { title = title, message = message });

    private static OnPasswordSubmitResponse Create(string nonce, OnPasswordSubmitAction action) =>
        new()
        {
            data = new OnPasswordSubmitResponseData
            {
                nonce = nonce,
                actions = [action]
            }
        };
}
