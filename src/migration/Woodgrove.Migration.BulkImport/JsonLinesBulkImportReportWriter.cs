using System.Text.Json;

namespace Woodgrove.Migration.BulkImport;

public sealed class JsonLinesBulkImportReportWriter : IBulkImportReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> WriteAsync(IReadOnlyCollection<BulkImportReportEntry> entries, CancellationToken ct = default)
    {
        var reportDirectory = Path.Combine(AppContext.BaseDirectory, "reports");
        Directory.CreateDirectory(reportDirectory);

        var reportPath = Path.Combine(reportDirectory, $"bulk-import-report-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.jsonl");
        await using var stream = File.Create(reportPath);
        await using var writer = new StreamWriter(stream);

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            var line = JsonSerializer.Serialize(entry, SerializerOptions);
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }

        return reportPath;
    }
}