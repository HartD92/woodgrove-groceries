using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Web;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System;
using Microsoft.Graph;

namespace woodgrovedemo.Helpers
{
    public class MsalAccessTokenHandler
    {
        private static readonly ConcurrentDictionary<string, Lazy<IConfidentialClientApplication>> ConfidentialClientApplications = new();

        public static X509Certificate2 ReadCertificate(string certificateThumbprint)
        {
            if (string.IsNullOrWhiteSpace(certificateThumbprint))
            {
                throw new ArgumentException("certificateThumbprint should not be empty. Please set the certificateThumbprint setting in the appsettings.json", "certificateThumbprint");
            }
            CertificateDescription certificateDescription = CertificateDescription.FromStoreWithThumbprint(
                 certificateThumbprint,
                 StoreLocation.CurrentUser,
                 StoreName.My);

            DefaultCertificateLoader defaultCertificateLoader = new DefaultCertificateLoader();
            defaultCertificateLoader.LoadIfNeeded(certificateDescription);

            if (certificateDescription.Certificate == null)
            {
                throw new Exception("Cannot find the certificate.");
            }

            return certificateDescription.Certificate;
        }

        public static  GraphServiceClient GetGraphClient(IConfiguration configuration, string[]? scopes = null)
        {
            if (scopes == null)
            {
                scopes = new string[] { "https://graph.microsoft.com/.default" };
            }

            var graphClient = new GraphServiceClient(new MsalAuthenticationProvider(configuration, scopes));

            return graphClient;
        }

        public static async Task<string> AcquireToken(IConfiguration configuration)
        {
            // Aquire an access token which will be sent as bearer to the request API
            var accessToken = await MsalAccessTokenHandler.GetAccessToken(configuration);
            if (accessToken.Item1 == String.Empty)
            {
                throw new Exception(String.Format("Failed to acquire access token: {0} : {1}", accessToken.error, accessToken.error_description));
            }

            return accessToken.Item1;
        }

        public static async Task<(string token, string error, string error_description)> GetAccessToken(IConfiguration configuration, string[]? scopes = null)
        {
            if (scopes == null)
            {
                scopes = new string[] { "https://graph.microsoft.com/.default" };
            }

            try
            {
                string? tenantId = GetConfiguredValue(configuration.GetSection("MicrosoftGraph:TenantId").Value);
                string? clientId = GetConfiguredValue(configuration.GetSection("MicrosoftGraph:ClientId").Value);
                string? clientSecret = GetConfiguredValue(configuration.GetSection("MicrosoftGraph:ClientSecret").Value);

                if (!string.IsNullOrWhiteSpace(clientSecret))
                {
                    if (string.IsNullOrWhiteSpace(tenantId))
                    {
                        throw new ArgumentNullException(nameof(tenantId), "MicrosoftGraph:TenantId cannot be null or empty.");
                    }

                    if (string.IsNullOrWhiteSpace(clientId))
                    {
                        throw new ArgumentNullException(nameof(clientId), "MicrosoftGraph:ClientId cannot be null or empty.");
                    }

                    var app = GetConfidentialClientApplicationWithSecret(clientId, clientSecret, tenantId);
                    var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
                    return (result.AccessToken, String.Empty, String.Empty);
                }

                if (string.IsNullOrWhiteSpace(tenantId))
                {
                    throw new ArgumentNullException(nameof(tenantId), "MicrosoftGraph:TenantId cannot be null or empty.");
                }

                if (string.IsNullOrWhiteSpace(clientId))
                {
                    throw new ArgumentNullException(nameof(clientId), "MicrosoftGraph:ClientId cannot be null or empty.");
                }

                string? certificateThumbprint = GetConfiguredValue(configuration.GetSection("MicrosoftGraph:CertificateThumbprint").Value);
                if (string.IsNullOrWhiteSpace(certificateThumbprint))
                {
                    throw new ArgumentNullException(nameof(certificateThumbprint), "Configure MicrosoftGraph:ClientSecret or MicrosoftGraph:CertificateThumbprint.");
                }

                X509Certificate2 certificate = ReadCertificate(certificateThumbprint);
                var certificateApp = GetConfidentialClientApplicationWithCertificate(clientId, certificate, tenantId);
                var certificateResult = await certificateApp.AcquireTokenForClient(scopes).ExecuteAsync();

                return (certificateResult.AccessToken, String.Empty, String.Empty);
            }
            catch (Exception ex)
            {
                return (String.Empty, "500", "Something went wrong getting an access token for the client API:" + ex.Message);
            }
        }

        private static IConfidentialClientApplication GetConfidentialClientApplicationWithSecret(string clientId, string clientSecret, string tenantId)
        {
            string cacheKey = $"secret:{tenantId}:{clientId}";

            return ConfidentialClientApplications.GetOrAdd(
                cacheKey,
                _ => new Lazy<IConfidentialClientApplication>(() =>
                    ConfidentialClientApplicationBuilder
                        .Create(clientId)
                        .WithClientSecret(clientSecret)
                        .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}/v2.0"))
                        .Build()))
                .Value;
        }

        private static IConfidentialClientApplication GetConfidentialClientApplicationWithCertificate(string clientId, X509Certificate2 certificate, string tenantId)
        {
            string cacheKey = $"certificate:{tenantId}:{clientId}:{certificate.Thumbprint}";

            return ConfidentialClientApplications.GetOrAdd(
                cacheKey,
                _ => new Lazy<IConfidentialClientApplication>(() =>
                    ConfidentialClientApplicationBuilder
                        .Create(clientId)
                        .WithCertificate(certificate)
                        .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}/v2.0"))
                        .Build()))
                .Value;
        }

        private static string? GetConfiguredValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("<", StringComparison.Ordinal))
            {
                return null;
            }

            return value;
        }

        private sealed class MsalAuthenticationProvider : IAuthenticationProvider
        {
            private readonly IConfiguration _configuration;
            private readonly string[] _scopes;

            public MsalAuthenticationProvider(IConfiguration configuration, string[] scopes)
            {
                _configuration = configuration;
                _scopes = scopes;
            }

            public async Task AuthenticateRequestAsync(
                RequestInformation request,
                Dictionary<string, object>? additionalAuthenticationContext = null,
                CancellationToken cancellationToken = default)
            {
                var accessToken = await GetAccessToken(_configuration, _scopes);
                if (accessToken.token == String.Empty)
                {
                    throw new InvalidOperationException(String.Format("Failed to acquire access token: {0} : {1}", accessToken.error, accessToken.error_description));
                }

                if (request.Headers.ContainsKey("Authorization"))
                {
                    request.Headers.Remove("Authorization");
                }
                request.Headers.Add("Authorization", $"Bearer {accessToken.token}");
            }
        }

    }
}
