using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using woodgroveapi.Helpers;
using woodgroveapi.Models;

namespace woodgroveapi.Controllers;


[Authorize(AuthenticationSchemes = "EntraExternalIdCustomAuthToken")]
[ApiController]
[Route("[controller]")]
public class OnPageRenderStartController : ControllerBase
{
    private readonly ILogger<OnPageRenderStartController> _logger;
    private readonly TelemetryClient _telemetry;
    private readonly IConfiguration _configuration;

    public OnPageRenderStartController(ILogger<OnPageRenderStartController> logger, TelemetryClient telemetry, IConfiguration configuration)
    {
        _logger = logger;
        _telemetry = telemetry;
        _configuration = configuration;
    }

    [HttpPost(Name = "OnPageRenderStart")]
    public PageRenderStartResponse PostAsync([FromBody] PageRenderStartRequest requestPayload)
    {
        //For Azure App Service with Easy Auth, validate the azp claim value
        //if (!AzureAppServiceClaimsHeader.Authorize(this.Request))
        //{
        //     Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        //     return null;
        //}

        // Track the page view 
        IDictionary<string, string> moreProperties = new Dictionary<string, string>();
        moreProperties.Add("Page", requestPayload.data.pageId);
        AppInsightsHelper.TrackApi("OnPageRenderStart", this._telemetry, requestPayload.data, moreProperties);

        PageRenderStartResponse r = new PageRenderStartResponse();
        r.type = requestPayload.type;
        r.source = requestPayload.source;

        var branding = r.data.actions[0].tenantBranding;
        string appUrl = "";
        string welcome = "";

        switch (requestPayload.data.authenticationContext.clientServicePrincipal.appId)
        {
            case "7a30b8ed-42a3-4d1e-89ad-14d4ca3c9a52":
                appUrl = "https://woodgrovebanking.com";
                welcome = "**Woodgrove online bank**";
                break;

            case "65d59577-c9d1-485b-87a5-80b92a99fbfa":
                appUrl = "https://woodgroverestaurants.com";
                welcome = "**Woodgrove restaurant**";
                break;

            default:
                appUrl = "https://woodgrovedemo.com";
                welcome = "**Woodgrove groceries** online store";
                break;
        }

        r.data.actions[0].tenantBranding = RetrieveBranding(appUrl, welcome);
        return r;
    }

    private PageRenderStartResponse_TenantBranding RetrieveBranding(string appUrl, string welcome)
    {
        PageRenderStartResponse_TenantBranding branding = new PageRenderStartResponse_TenantBranding();
        var externalAssetBaseUrl = GetBrandAssetsBaseUrl();

        branding.backgroundColor = "#343434";
        branding.customCSS = $"{appUrl}/Company-branding/af-custom.css";

        // Header
        branding.loginPageLayoutConfiguration = new PageRenderStartResponse_LoginPageLayoutConfiguration();
        branding.loginPageLayoutConfiguration.isHeaderShown = true;
        branding.loginPageLayoutConfiguration.isFooterShown = true;
        branding.headerBackgroundColor = "#223846";
        branding.headerLogo = GetBrandAssetUrl(externalAssetBaseUrl, "af-logo-light.svg");

        // Sign in box
        branding.usernameHintText = "Email address";
        branding.signInPageText = $"Welcome to the **ABERCROMBIE & FITCH** demo sign-in for {welcome}. Sign in with your credentials, create an account, or continue with an available social identity. For help, please [contact the Woodgrove demo team](https://woodgrovedemo.com/help).";
        branding.bannerLogo = GetBrandAssetUrl(externalAssetBaseUrl, "af-logo.svg");
        branding.squareLogo = GetBrandAssetUrl(externalAssetBaseUrl, "af-square-logo-light.png");
        branding.squareLogoDark = GetBrandAssetUrl(externalAssetBaseUrl, "af-square-logo-dark.png");
        branding.backgroundImage = GetBrandAssetUrl(externalAssetBaseUrl, "af-background.jpg");
        branding.favicon = GetBrandAssetUrl(externalAssetBaseUrl, "af-favicon.png");

        // Terms of use
        branding.customTermsOfUseText = "Woodgrove terms of use";
        branding.customTermsOfUseUrl = $"{appUrl}/tos";

        // Privacy & Cookies statement
        branding.customPrivacyAndCookiesText = "Privacy & Cookies statement";
        branding.customPrivacyAndCookiesUrl = $"{appUrl}/privacy";

        //branding.contentCustomization = new PageRenderStartResponse_ContentCustomization();
        // branding.contentCustomization.attributeCollection= new PageRenderStartResponse_AttributeCollection();
        // branding.contentCustomization.attributeCollection.signIn_Description = "This is my test";
        // branding.contentCustomization.attributeCollection.signIn_Title = "This is my test";


        //branding.contentCustomization.attributeCollection = "[{\"key\": \"SignIn_Description\", \"value\": \"This is my test\" },  {  \"key\": \"SignIn_Title\", \"value\": \"This is my test\" }]";



        // branding.contentCustomization.attributeCollection = new List<PageRenderStartResponse_AttributeCollection>();
        // branding.contentCustomization.attributeCollection.Add( new PageRenderStartResponse_AttributeCollection("SignIn_Description", "This is my test"));
        // branding.contentCustomization.attributeCollection.Add( new PageRenderStartResponse_AttributeCollection("SignIn_Title", "This is my test"));

        return branding;

    }

    private string? GetBrandAssetsBaseUrl()
    {
        var configuredBaseUrl = _configuration["BrandAssets:BaseUrl"];

        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            configuredBaseUrl = _configuration["BRAND_ASSETS_BASE_URL"];
        }

        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return null;
        }

        return configuredBaseUrl.Trim().TrimEnd('/');
    }

    private static string? GetBrandAssetUrl(string? baseUrl, string fileName)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        return $"{baseUrl}/{fileName}";
    }
}