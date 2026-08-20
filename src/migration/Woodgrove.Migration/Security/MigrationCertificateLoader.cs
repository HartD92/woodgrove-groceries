using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Web;
using System.Security.Cryptography.X509Certificates;
using Woodgrove.Migration.Options;

namespace Woodgrove.Migration.Security;

public static class MigrationCertificateLoader
{
    public static X509Certificate2 ReadCertificate(string certificateThumbprint)
    {
        if (string.IsNullOrWhiteSpace(certificateThumbprint))
        {
            throw new ArgumentException("certificateThumbprint should not be empty.", nameof(certificateThumbprint));
        }

        CertificateDescription certificateDescription = CertificateDescription.FromStoreWithThumbprint(
            certificateThumbprint,
            StoreLocation.CurrentUser,
            StoreName.My);

        DefaultCertificateLoader defaultCertificateLoader = new();
        defaultCertificateLoader.LoadIfNeeded(certificateDescription);

        return certificateDescription.Certificate
            ?? throw new InvalidOperationException("Cannot find the certificate.");
    }

    public static X509Certificate2 LoadFromConfiguration(IConfiguration configuration)
    {
        var options = configuration.GetSection(MigrationOptions.SectionName).Get<MigrationOptions>() ?? new MigrationOptions();
        return Load(options);
    }

    public static X509Certificate2 Load(MigrationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.JitEncryptionCertificateBase64Pfx))
        {
            return X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(options.JitEncryptionCertificateBase64Pfx),
                options.JitEncryptionCertificatePassword,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
        }

        if (!string.IsNullOrWhiteSpace(options.JitEncryptionCertificateThumbprint))
        {
            return ReadCertificate(options.JitEncryptionCertificateThumbprint);
        }

        throw new InvalidOperationException("Configure Migration:JitEncryptionCertificateBase64Pfx or Migration:JitEncryptionCertificateThumbprint.");
    }
}
