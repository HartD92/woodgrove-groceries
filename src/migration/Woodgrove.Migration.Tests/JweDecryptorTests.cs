using Jose;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Woodgrove.Migration.Security;
using Xunit;

namespace Woodgrove.Migration.Tests;

public class JweDecryptorTests
{
    [Fact]
    public void Decrypt_RoundTripsPayload()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=JitMigration", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var certWithKey = X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), password: null);

        var payload = JsonSerializer.Serialize(new PasswordContextClaims
        {
            Username = "ada@example.com",
            UserPassword = "P@ssw0rd123!",
            Nonce = "nonce-123"
        });

        var encrypted = JWT.Encode(payload, certWithKey.GetRSAPublicKey()!, JweAlgorithm.RSA_OAEP_256, JweEncryption.A256GCM);

        var decryptor = new JweDecryptor();
        var claims = decryptor.Decrypt(encrypted, certWithKey);

        Assert.Equal("ada@example.com", claims.Username);
        Assert.Equal("P@ssw0rd123!", claims.UserPassword);
        Assert.Equal("nonce-123", claims.Nonce);
    }
}
