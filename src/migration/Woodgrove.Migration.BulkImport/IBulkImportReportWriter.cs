namespace Woodgrove.Migration.BulkImport;

public interface IBulkImportReportWriter
{
    Task<string> WriteAsync(IReadOnlyCollection<BulkImportReportEntry> entries, CancellationToken ct = default);
}