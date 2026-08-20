using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Woodgrove.Migration.Abstractions;
using Woodgrove.Migration.BulkImport;
using Woodgrove.Migration.Graph;
using Woodgrove.Migration.Mock;
using Woodgrove.Migration.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

builder.Services
    .AddOptions<MigrationOptions>()
    .Bind(builder.Configuration.GetSection(MigrationOptions.SectionName));

builder.Services.AddSingleton<GraphMigrationClient>();
builder.Services.AddSingleton<IGraphMigrationService, GraphMigrationServiceAdapter>();
builder.Services.AddSingleton<ILegacyIdentityProvider, MockLegacyIdentityProvider>();
builder.Services.AddSingleton<IBulkImportPasswordGenerator, BulkImportPasswordGenerator>();
builder.Services.AddSingleton<IBulkImportReportWriter, JsonLinesBulkImportReportWriter>();
builder.Services.AddSingleton<BulkImportRunner>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var runner = scope.ServiceProvider.GetRequiredService<BulkImportRunner>();
var summary = await runner.RunAsync();

Console.WriteLine($"Bulk import complete. {summary.Succeeded} succeeded, {summary.Failed} failed. Report: {summary.ReportPath}");
return summary.Failed == 0 ? 0 : 1;