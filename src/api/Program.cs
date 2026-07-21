using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.Identity.Abstractions;
using Microsoft.Extensions.Logging.AzureAppServices;

var builder = WebApplication.CreateBuilder(args);

// Add Azure stream log service
builder.Logging.AddAzureWebAppDiagnostics();
builder.Services.Configure<AzureFileLoggerOptions>(options =>
{
    options.FileName = "azure-diagnostics-";
    options.FileSizeLimit = 50 * 1024;
    options.RetainedFileCountLimit = 5;
});
builder.Logging.AddFilter((provider, category, logLevel) =>
{
    return provider.ToLower().Contains("woodgroveapi");
});

ConfigurationSection WoodgroveGroceriesDownstreamApi = (ConfigurationSection)builder.Configuration.GetSection("WoodgroveGroceriesDownstreamApi");

// Add authentication scheme
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddDownstreamApi("WoodgroveGroceriesDownstreamApi", WoodgroveGroceriesDownstreamApi)
    .AddInMemoryTokenCaches();

builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                                                 options =>
                                                 {
                                                     options.TokenValidationParameters.NameClaimType = "name";
                                                 });
// Add in memory cache                                                  
builder.Services.AddMemoryCache();

// Add API controllers
builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
