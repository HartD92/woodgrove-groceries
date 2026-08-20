using Microsoft.Extensions.Logging.Abstractions;
using Woodgrove.Migration.Abstractions;
using Woodgrove.Migration.BulkImport;
using Xunit;

namespace Woodgrove.Migration.BulkImport.Tests;

public class BulkImportRunnerTests
{
    [Fact]
    public async Task RunAsync_ContinuesAfterPerUserFailure()
    {
        var users = new[]
        {
            new LegacyUserRecord("legacy-1", "one@example.com", "One"),
            new LegacyUserRecord("legacy-2", "two@example.com", "Two"),
            new LegacyUserRecord("legacy-3", "three@example.com", "Three")
        };

        var service = new FakeGraphMigrationService(record => record.LegacyUserId == "legacy-2" ? throw new InvalidOperationException("boom") : $"object-{record.LegacyUserId}");
        var reportWriter = new InMemoryReportWriter();
        var runner = new BulkImportRunner(service, new FakeLegacyIdentityProvider(users), new FixedPasswordGenerator(), reportWriter, NullLogger<BulkImportRunner>.Instance);

        var summary = await runner.RunAsync();

        Assert.Equal(2, summary.Succeeded);
        Assert.Equal(1, summary.Failed);
        Assert.Collection(reportWriter.Entries,
            entry => Assert.Equal("Succeeded", entry.Status),
            entry =>
            {
                Assert.Equal("Failed", entry.Status);
                Assert.Equal("boom", entry.Error);
            },
            entry => Assert.Equal("Succeeded", entry.Status));
    }

    private sealed class FakeLegacyIdentityProvider(IEnumerable<LegacyUserRecord> users) : ILegacyIdentityProvider
    {
        public async IAsyncEnumerable<LegacyUserRecord> EnumerateUsersAsync(CancellationToken ct = default)
        {
            foreach (var user in users)
            {
                yield return user;
                await Task.Yield();
            }
        }

        public Task<LegacyValidationResult> ValidateAsync(string usernameOrEmail, string? plaintextPassword, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FixedPasswordGenerator : IBulkImportPasswordGenerator
    {
        public string Generate() => "TempP@ssw0rd123!";
    }

    private sealed class InMemoryReportWriter : IBulkImportReportWriter
    {
        public IReadOnlyList<BulkImportReportEntry> Entries { get; private set; } = [];

        public Task<string> WriteAsync(IReadOnlyCollection<BulkImportReportEntry> entries, CancellationToken ct = default)
        {
            Entries = entries.ToList();
            return Task.FromResult("report.jsonl");
        }
    }

    private sealed class FakeGraphMigrationService(Func<LegacyUserRecord, string> create) : IGraphMigrationService
    {
        public Task EnsureMigrationExtensionPropertyAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<string> CreateMigratedUserAsync(LegacyUserRecord record, string randomPassword, CancellationToken ct = default) =>
            Task.FromResult(create(record));
    }
}