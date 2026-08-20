namespace Woodgrove.Migration.BulkImport;

public sealed record BulkImportSummary(string ReportPath, int Succeeded, int Failed)
{
    public static BulkImportSummary FromEntries(string reportPath, IReadOnlyCollection<BulkImportReportEntry> entries) =>
        new(reportPath, entries.Count(entry => entry.Status == "Succeeded"), entries.Count(entry => entry.Status == "Failed"));
}