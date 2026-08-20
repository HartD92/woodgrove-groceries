public enum AuthMethodType
{
    SignInEmail,
    EmailMfa,
    PhoneMfa
}

public class AuthMethod
{
    public string UID { get; set; } = string.Empty;
    public string AuthValue { get; set; } = string.Empty;
    public AuthMethodType AuthType { get; set; }
    public string VerificationCode { get; set; } = string.Empty;
    public int MessagesSent { get; set; }
    public int Validations { get; set; }
}
