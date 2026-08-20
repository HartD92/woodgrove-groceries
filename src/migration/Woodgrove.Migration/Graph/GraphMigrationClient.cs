using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Security.Cryptography.X509Certificates;
using Woodgrove.Migration.Abstractions;
using Woodgrove.Migration.Options;
using Woodgrove.Migration.Security;

namespace Woodgrove.Migration.Graph;

public sealed class GraphMigrationClient
{
    private readonly GraphServiceClient _graphClient;
    private readonly MigrationOptions _options;
    private readonly GraphRetryPolicy _retryPolicy;

    public GraphMigrationClient(IConfiguration configuration)
        : this(
            new GraphServiceClient(CreateGraphCredential(configuration), ["https://graph.microsoft.com/.default"]),
            configuration.GetSection(MigrationOptions.SectionName).Get<MigrationOptions>() ?? new MigrationOptions())
    {
    }

    public GraphMigrationClient(GraphServiceClient graphClient, MigrationOptions options, GraphRetryPolicy? retryPolicy = null)
    {
        _graphClient = graphClient;
        _options = options;
        _retryPolicy = retryPolicy ?? new GraphRetryPolicy(options.MaxGraphRetryAttempts);
    }

    public async Task EnsureMigrationExtensionPropertyAsync(CancellationToken ct = default)
    {
        var extensionName = GetExtensionPropertyName();
        var application = await FindExtensionsApplicationAsync(ct).ConfigureAwait(false);

        var existing = await _retryPolicy.ExecuteAsync(
            token => _graphClient.Applications[application.Id!].ExtensionProperties.GetAsync(
                requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Filter = $"name eq '{extensionName}'";
                },
                cancellationToken: token),
            ct).ConfigureAwait(false);

        if (existing?.Value?.Any() == true)
        {
            return;
        }

        var extensionProperty = new ExtensionProperty
        {
            Name = extensionName,
            DataType = "Boolean",
            TargetObjects = ["User"]
        };

        await _retryPolicy.ExecuteAsync(
            token => _graphClient.Applications[application.Id!].ExtensionProperties.PostAsync(extensionProperty, cancellationToken: token),
            ct).ConfigureAwait(false);
    }

    public async Task<string> CreateMigratedUserAsync(LegacyUserRecord record, string randomPassword, CancellationToken ct = default)
    {
        var user = new User
        {
            AccountEnabled = true,
            CreationType = "LocalAccount",
            DisplayName = record.DisplayName,
            GivenName = record.GivenName,
            Surname = record.Surname,
            Mail = record.Email,
            Identities =
            [
                new ObjectIdentity
                {
                    SignInType = "emailAddress",
                    IssuerAssignedId = record.Email
                }
            ],
            PasswordProfile = new PasswordProfile
            {
                Password = randomPassword,
                ForceChangePasswordNextSignIn = false
            },
            AdditionalData = new Dictionary<string, object>
            {
                [GetExtensionPropertyName()] = true
            }
        };

        var created = await _retryPolicy.ExecuteAsync(
            token => _graphClient.Users.PostAsync(user, cancellationToken: token),
            ct).ConfigureAwait(false);

        return created?.Id ?? throw new InvalidOperationException("Graph did not return a user id.");
    }

    public Task MarkUserMigratedAsync(string userId, CancellationToken ct = default)
    {
        var patch = new User
        {
            AdditionalData = new Dictionary<string, object>
            {
                [GetExtensionPropertyName()] = false
            }
        };

        return _retryPolicy.ExecuteAsync(
            token => _graphClient.Users[userId].PatchAsync(patch, cancellationToken: token),
            ct);
    }

    public string GetExtensionPropertyName()
    {
        if (string.IsNullOrWhiteSpace(_options.B2CExtensionsAppId))
        {
            throw new InvalidOperationException("Migration:B2CExtensionsAppId must be configured.");
        }

        var normalizedAppId = _options.B2CExtensionsAppId.Replace("-", string.Empty, StringComparison.Ordinal);
        return $"extension_{normalizedAppId}_{_options.MigrationExtensionPropertyName}";
    }

    private async Task<Application> FindExtensionsApplicationAsync(CancellationToken ct)
    {
        var application = await _retryPolicy.ExecuteAsync(
            token => _graphClient.Applications.GetAsync(
                requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Filter = $"appId eq '{_options.B2CExtensionsAppId}'";
                    requestConfiguration.QueryParameters.Select = ["id", "appId"];
                    requestConfiguration.QueryParameters.Top = 1;
                },
                cancellationToken: token),
            ct).ConfigureAwait(false);

        return application?.Value?.SingleOrDefault()
            ?? throw new InvalidOperationException("Could not find b2c-extensions-app application registration.");
    }

    private static TokenCredential CreateGraphCredential(IConfiguration configuration)
    {
        string? tenantId = GetConfiguredValue(
            configuration.GetSection("MicrosoftGraph:TenantId").Value,
            configuration.GetSection("AzureAd:TenantId").Value);
        string? clientId = GetConfiguredValue(
            configuration.GetSection("MicrosoftGraph:ClientId").Value,
            configuration.GetSection("AzureAd:ClientId").Value);
        string? clientSecret = GetConfiguredValue(configuration.GetSection("MicrosoftGraph:ClientSecret").Value);

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentNullException(nameof(tenantId), "MicrosoftGraph:TenantId or AzureAd:TenantId cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentNullException(nameof(clientId), "MicrosoftGraph:ClientId or AzureAd:ClientId cannot be null or empty.");
        }

        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            return new ClientSecretCredential(tenantId, clientId, clientSecret);
        }

        string? certificateThumbprint = GetConfiguredValue(
            configuration.GetSection("MicrosoftGraph:CertificateThumbprint").Value,
            configuration.GetSection("AzureAd:ClientCertificates:0:CertificateThumbprint").Value);
        if (string.IsNullOrWhiteSpace(certificateThumbprint))
        {
            throw new ArgumentNullException(nameof(certificateThumbprint), "Configure MicrosoftGraph:ClientSecret or a certificate thumbprint.");
        }

        X509Certificate2 certificate = MigrationCertificateLoader.ReadCertificate(certificateThumbprint);
        return new ClientCertificateCredential(tenantId, clientId, certificate);
    }

    private static string? GetConfiguredValue(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("<", StringComparison.Ordinal))
            {
                return value;
            }
        }

        return null;
    }
}
