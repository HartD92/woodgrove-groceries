using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;

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

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Woodgrove Groceries Graph middleware web API",
        Description = "This dotnet Web API entpoint demonstrate how to secure access to Microsoft Graph via a middleware. Checkout the [source code](https://github.com/microsoft/woodgrove-groceries-graph-middleware) <br> <br> Assembly version " + System.Reflection.Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "Unknown version",
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.RoutePrefix = string.Empty;
});
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
