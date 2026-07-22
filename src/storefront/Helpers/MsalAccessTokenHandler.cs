using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Web;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System;
using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;

namespace woodgrovedemo.Helpers
{
    public class MsalAccessTokenHandler
    {
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

            var graphClient = new GraphServiceClient(CreateGraphCredential(configuration), scopes);

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
                AccessToken result = await CreateGraphCredential(configuration).GetTokenAsync(
                    new TokenRequestContext(scopes),
                    CancellationToken.None);

                return (result.Token, String.Empty, String.Empty);
            }
            catch (Exception ex)
            {
                return (String.Empty, "500", "Something went wrong getting an access token for the client API:" + ex.Message);
            }
        }

        private static TokenCredential CreateGraphCredential(IConfiguration configuration)
        {
            string? tenantId = GetConfiguredValue(configuration.GetSection("MicrosoftGraph:TenantId").Value);
            string? clientId = GetConfiguredValue(configuration.GetSection("MicrosoftGraph:ClientId").Value);
            string? clientSecret = GetConfiguredValue(configuration.GetSection("MicrosoftGraph:ClientSecret").Value);

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentNullException(nameof(tenantId), "MicrosoftGraph:TenantId cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentNullException(nameof(clientId), "MicrosoftGraph:ClientId cannot be null or empty.");
            }

            if (!string.IsNullOrWhiteSpace(clientSecret))
            {
                return new ClientSecretCredential(tenantId, clientId, clientSecret);
            }

            string? certificateThumbprint = GetConfiguredValue(configuration.GetSection("MicrosoftGraph:CertificateThumbprint").Value);
            if (string.IsNullOrWhiteSpace(certificateThumbprint))
            {
                throw new ArgumentNullException(nameof(certificateThumbprint), "Configure MicrosoftGraph:ClientSecret or MicrosoftGraph:CertificateThumbprint.");
            }

            X509Certificate2 certificate = ReadCertificate(certificateThumbprint);
            return new ClientCertificateCredential(tenantId, clientId, certificate);
        }

        private static string? GetConfiguredValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("<", StringComparison.Ordinal))
            {
                return null;
            }

            return value;
        }

    }
}
