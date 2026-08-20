namespace Woodgrove.Migration.Options;

public sealed class MigrationOptions
{
    public const string SectionName = "Migration";

    public string? JitEncryptionCertificateThumbprint { get; set; }

    public string? JitEncryptionCertificateBase64Pfx { get; set; }

    public string? JitEncryptionCertificatePassword { get; set; }

    public string? B2CExtensionsAppId { get; set; }

    public string MigrationExtensionPropertyName { get; set; } = "toBeMigrated";

    public int MaxGraphRetryAttempts { get; set; } = 3;
}
