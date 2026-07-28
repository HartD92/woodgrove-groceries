using System.Text.Json;

namespace woodgrovedemo.Models;

public class PasskeyRegisterRequest
{
    public JsonElement PublicKeyCredential { get; set; }
    public string? DisplayName { get; set; }
}
