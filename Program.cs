using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
ConfigurationSection WoodgroveGroceriesDownstreamApi = (ConfigurationSection)builder.Configuration.GetSection("WoodgroveGroceriesDownstreamApi");

// Add authentication scheme
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddMicrosoftGraph(builder.Configuration.GetSection("GraphApi"))
    .AddInMemoryTokenCaches();

builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                                                 options =>
                                                 {
                                                     options.TokenValidationParameters.NameClaimType = "name";
                                                 });
builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
