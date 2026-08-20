using Microsoft.Extensions.Logging;
using Woodgrove.Migration.Abstractions;

namespace Woodgrove.Migration.BulkImport;

public sealed class BulkImportRunner
{
    private readonly IGraphMigrationService _graphMigrationService;
    private readonly ILegacyIdentityProvider _legacyIdentityProvider;
    private readonly IBulkImportPasswordGenerator _passwordGenerator;
    private readonly IBulkImportReportWriter _reportWriter;
    private readonly ILogger<BulkImportRunner> _logger;

    public BulkImportRunner(
        IGraphMigrationService graphMigrationService,
        ILegacyIdentityProvider legacyIdentityProvider,
        IBulkImportPasswordGenerator passwordGenerator,
        IBulkImportReportWriter reportWriter,
        ILogger<BulkImportRunner> logger)
    {
        _graphMigrationService = graphMigrationService;
        _legacyIdentityProvider = legacyIdentityProvider;
        _passwordGenerator = passwordGenerator;
        _reportWriter = reportWriter;
        _logger = logger;
    }

    public async Task<BulkImportSummary> RunAsync(CancellationToken ct = default)
    {
        await _graphMigrationService.EnsureMigrationExtensionPropertyAsync(ct).ConfigureAwait(false);

        List<BulkImportReportEntry> entries = [];

        await foreach (var record in _legacyIdentityProvider.EnumerateUsersAsync(ct).ConfigureAwait(false))
        {
            try
            {
                var randomPassword = _passwordGenerator.Generate();
                var externalIdObjectId = await _graphMigrationService.CreateMigratedUserAsync(record, randomPassword, ct).ConfigureAwait(false);
                entries.Add(BulkImportReportEntry.Success(record.LegacyUserId, record.Email, externalIdObjectId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate legacy user {LegacyUserId}", record.LegacyUserId);
                entries.Add(BulkImportReportEntry.Failure(record.LegacyUserId, record.Email, ex.Message));
            }
        }

        var reportPath = await _reportWriter.WriteAsync(entries, ct).ConfigureAwait(false);
        return BulkImportSummary.FromEntries(reportPath, entries);
    }
}