using System.Text.Json;
using Woodgrove.Migration.BulkImport;
using Xunit;

namespace Woodgrove.Migration.BulkImport.Tests;

public class JsonLinesBulkImportReportWriterTests
{
    [Fact]
    public async Task WriteAsync_WritesJsonLinePerEntry()
    {
        var writer = new JsonLinesBulkImportReportWriter();
        var entries = new[]
        {
            BulkImportReportEntry.Success("legacy-ada", "ada@example.com", "object-1"),
            BulkImportReportEntry.Failure("legacy-alan", "alan@example.com", "throttled")
        };

        var reportPath = await writer.WriteAsync(entries);

        Assert.True(File.Exists(reportPath));
        var lines = await File.ReadAllLinesAsync(reportPath);
        Assert.Equal(2, lines.Length);

        using var document = JsonDocument.Parse(lines[0]);
        Assert.Equal("legacy-ada", document.RootElement.GetProperty("legacyUserId").GetString());
        Assert.Equal("Succeeded", document.RootElement.GetProperty("status").GetString());

        File.Delete(reportPath);
        var directory = Path.GetDirectoryName(reportPath)!;
        if (!Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }
}