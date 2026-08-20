using Woodgrove.Migration.Abstractions;
using Woodgrove.Migration.Graph;

namespace Woodgrove.Migration.BulkImport;

public sealed class GraphMigrationServiceAdapter : IGraphMigrationService
{
    private readonly GraphMigrationClient _graphMigrationClient;

    public GraphMigrationServiceAdapter(GraphMigrationClient graphMigrationClient)
    {
        _graphMigrationClient = graphMigrationClient;
    }

    public Task EnsureMigrationExtensionPropertyAsync(CancellationToken ct = default) =>
        _graphMigrationClient.EnsureMigrationExtensionPropertyAsync(ct);

    public Task<string> CreateMigratedUserAsync(LegacyUserRecord record, string randomPassword, CancellationToken ct = default) =>
        _graphMigrationClient.CreateMigratedUserAsync(record, randomPassword, ct);
}
