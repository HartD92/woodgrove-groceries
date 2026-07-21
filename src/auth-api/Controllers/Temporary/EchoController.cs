using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using woodgroveapi.Helpers;
using woodgroveapi.Models;

namespace woodgroveapi.Controllers;


//[Authorize]
[ApiController]
[Route("[controller]")]
[DevelopmentOnly]
public class EchoController : ControllerBase
{
    private readonly ILogger<EchoController> _logger;
    private TelemetryClient _telemetry;


    public EchoController(ILogger<EchoController> logger, TelemetryClient telemetry)
    {
        _logger = logger;
        _telemetry = telemetry;
    }

    [HttpPost(Name = "Echo")]
    public async Task<object> PostAsync()
    {
        // Track the page view 
        PageViewTelemetry pageView = new PageViewTelemetry("Echo");
        _telemetry.TrackPageView(pageView);

        _logger.LogInformation($"#### call to: {this.GetType().Name}");

        // Echo the input data
        string requestBody = await new StreamReader(this.Request.Body).ReadToEndAsync();

        _logger.LogInformation($"#### {requestBody}");

        return "Echo";
    }
}

