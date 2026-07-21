using Microsoft.Extensions.Logging.AzureAppServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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
    return provider!.ToLower().Contains("woodgroveapi");
});

ConfigurationSection entraExternalIdCustomAuthTokenSettings = (ConfigurationSection)builder.Configuration.GetSection("EntraExternalIdCustomAuthToken");

// Reference: 
// There is an issue validating the first party token with 
// https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer
// https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication
builder.Services.AddAuthentication()
    .AddJwtBearer("EntraExternalIdCustomAuthToken", jwtOptions =>
    {
        jwtOptions.MetadataAddress = entraExternalIdCustomAuthTokenSettings["MetadataAddress"]!;
        jwtOptions.Audience = entraExternalIdCustomAuthTokenSettings["Audience"];
        jwtOptions.IncludeErrorDetails = true;
        jwtOptions.MapInboundClaims = false;
        jwtOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true
        };
        jwtOptions.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                // Validate the authorized party (the app who issued the token)
                string? clientappId = context?.Principal?.Claims.FirstOrDefault(x => x.Type == "azp" && x.Value == "99045fe1-7639-4a75-9d4a-577b6ca3810f")?.Value;
                if (clientappId == null)
                {
                    context!.Fail("Invalid azp claim value");
                }
                return Task.CompletedTask;
            }
        };
    });


ConfigurationSection entraExternalIdUserToken = (ConfigurationSection)builder.Configuration.GetSection("EntraExternalIdUserToken");
builder.Services.AddAuthentication()
    .AddJwtBearer("EntraExternalIdUserToken", jwtOptions =>
    {
        jwtOptions.MetadataAddress = entraExternalIdUserToken["MetadataAddress"]!;
        jwtOptions.Audience = entraExternalIdUserToken["Audience"];
        jwtOptions.IncludeErrorDetails = true;
        jwtOptions.MapInboundClaims = false;
        jwtOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true
        };
    });

// Add in memory cache                                                  
builder.Services.AddMemoryCache();

builder.Services.AddControllers();

// The following line enables Application Insights telemetry collection.
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
