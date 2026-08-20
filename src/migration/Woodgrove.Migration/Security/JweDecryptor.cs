using Jose;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Woodgrove.Migration.Security;

public sealed class JweDecryptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public PasswordContextClaims Decrypt(string encryptedPasswordContext, X509Certificate2 certificate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPasswordContext);
        ArgumentNullException.ThrowIfNull(certificate);

        using var rsa = certificate.GetRSAPrivateKey() ?? throw new InvalidOperationException("Certificate does not contain an RSA private key.");
        var json = JWT.Decode(encryptedPasswordContext, rsa);
        return JsonSerializer.Deserialize<PasswordContextClaims>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize decrypted password context.");
    }
}

public sealed class PasswordContextClaims
{
    [JsonPropertyName("user-password")]
    public string UserPassword { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = string.Empty;
}
