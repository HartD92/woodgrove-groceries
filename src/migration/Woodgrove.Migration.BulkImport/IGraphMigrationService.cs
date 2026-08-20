using Woodgrove.Migration.Abstractions;

namespace Woodgrove.Migration.BulkImport;

public interface IGraphMigrationService
{
    Task EnsureMigrationExtensionPropertyAsync(CancellationToken ct = default);

    Task<string> CreateMigratedUserAsync(LegacyUserRecord record, string randomPassword, CancellationToken ct = default);
}
