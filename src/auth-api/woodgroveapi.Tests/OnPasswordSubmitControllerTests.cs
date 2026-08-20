using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Jose;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Woodgrove.Migration.Contracts;
using Woodgrove.Migration.Mock;
using Woodgrove.Migration.Options;
using Woodgrove.Migration.Security;
using woodgroveapi.Controllers;
using Xunit;

namespace woodgroveapi.Tests;

public class OnPasswordSubmitControllerTests
{
    [Fact]
    public async Task OnPasswordSubmit_RequiresBearerToken()
    {
        using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.PostAsJsonAsync("/OnPasswordSubmit", new OnPasswordSubmitRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("ada@example.com", "P@ssw0rd123!", "user-ada", OnPasswordSubmitActionTypes.MigratePassword)]
    [InlineData("weak@example.com", "weakpass", "user-weak", OnPasswordSubmitActionTypes.UpdatePassword)]
    [InlineData("alan@example.com", "wrong-password", "user-alan", OnPasswordSubmitActionTypes.Retry)]
    [InlineData("locked@example.com", "CantUseThis1!", "user-lock", OnPasswordSubmitActionTypes.Block)]
    public async Task PostAsync_ReturnsExpectedAction(string username, string password, string userId, string expectedAction)
    {
        var certificateBytes = CreateCertificateBytes();
        using var certificate = LoadCertificate(certificateBytes);
        var controller = CreateController(certificateBytes, markUserMigratedAsync: (_, _) => Task.CompletedTask);

        var response = await controller.PostAsync(CreateRequest(certificate, username, password, userId), CancellationToken.None);

        var json = JsonSerializer.Serialize(response);
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement.GetProperty("data");
        Assert.Equal("nonce-test", data.GetProperty("nonce").GetString());
        Assert.Equal(expectedAction, data.GetProperty("actions")[0].GetProperty("@odata.type").GetString());
    }

    [Fact]
    public async Task PostAsync_MigratePassword_ContinuesWhenMigrationUpdateFails()
    {
        var certificateBytes = CreateCertificateBytes();
        using var certificate = LoadCertificate(certificateBytes);
        var controller = CreateController(certificateBytes, (_, _) => throw new InvalidOperationException("boom"));

        var response = await controller.PostAsync(CreateRequest(certificate, "ada@example.com", "P@ssw0rd123!", "user-ada"), CancellationToken.None);

        Assert.Equal(OnPasswordSubmitActionTypes.MigratePassword, response.data.actions[0].odatatype);
    }

    private static OnPasswordSubmitController CreateController(
        byte[] certificateBytes,
        Func<string, CancellationToken, Task> markUserMigratedAsync)
    {
        var options = Options.Create(new MigrationOptions
        {
            JitEncryptionCertificateBase64Pfx = Convert.ToBase64String(certificateBytes)
        });

        return new OnPasswordSubmitController(
            NullLogger<OnPasswordSubmitController>.Instance,
            new TelemetryClient(),
            new MockLegacyIdentityProvider(),
            options,
            new JweDecryptor(),
            markUserMigratedAsync);
    }

    private static OnPasswordSubmitRequest CreateRequest(
        X509Certificate2 certificate,
        string username,
        string password,
        string userId)
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["user-password"] = password,
            ["username"] = username,
            ["nonce"] = "nonce-test"
        });

        var encrypted = JWT.Encode(payload, certificate.GetRSAPublicKey()!, JweAlgorithm.RSA_OAEP_256, JweEncryption.A256GCM);

        return new OnPasswordSubmitRequest
        {
            source = "unit-test",
            data = new OnPasswordSubmitRequestData
            {
                encryptedPasswordContext = encrypted,
                tenantId = "tenant-id",
                authenticationEventListenerId = "listener-id",
                customAuthenticationExtensionId = "extension-id",
                authenticationContext = new OnPasswordSubmitAuthenticationContext
                {
                    correlationId = "correlation-id",
                    protocol = "OIDC",
                    client = new OnPasswordSubmitClientContext
                    {
                        ip = "127.0.0.1",
                        locale = "en-US",
                        market = "US"
                    },
                    user = new OnPasswordSubmitUserContext
                    {
                        id = userId,
                        mail = username,
                        displayName = "Test user"
                    }
                }
            }
        };
    }

    private static byte[] CreateCertificateBytes()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=JitMigrationTest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return certificate.Export(X509ContentType.Pfx);
    }

    private static X509Certificate2 LoadCertificate(byte[] certificateBytes)
    {
        return X509CertificateLoader.LoadPkcs12(certificateBytes, password: null);
    }
}
