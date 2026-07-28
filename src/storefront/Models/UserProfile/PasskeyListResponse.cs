namespace woodgrovedemo.Models;

public class PasskeyListResponse
{
    public string ErrorMessage { get; set; } = string.Empty;
    public List<PasskeyInfo> Passkeys { get; set; } = new();
}
