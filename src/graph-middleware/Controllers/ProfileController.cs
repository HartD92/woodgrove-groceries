using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Identity.Web;
using woodgrovedemo.Helpers;
using woodgrovedemo.Models;

namespace woodgrove_groceries_graph_middleware.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class ProfileController : ControllerBase
{

    // Dependency injection
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProfileController> _logger;
    private readonly GraphServiceClient _graphServiceClient;

    public ProfileController(ILogger<ProfileController> logger, IConfiguration configuration, GraphServiceClient graphServiceClient)
    {
        _logger = logger;
        _configuration = configuration;
        _graphServiceClient = graphServiceClient; ;
    }


    [HttpPost]
    public async Task<IActionResult> OnPostAsync([FromForm] UserAttributes att)
    {

        // Get the user unique identifier
        string? userObjectId = User.GetObjectId();

        if (userObjectId == null)
        {
            att.ErrorMessage = "The account cannot be updated since your access token doesn't contain the required 'objectidentifier' claim.";
        }

        try
        {
            // Update user by object ID
            var requestBody = new User();

            // A variable to count the number of attributes that are set to be updated
            int count = 0;

            // Check the display name and set it to the request body
            if (att.DontSkipEmptyString || string.IsNullOrEmpty(att.DisplayName) == false)
            {
                requestBody.DisplayName = att.DisplayName;
                count++;
            }

            // Check the given name and set it to the request body
            if (att.DontSkipEmptyString || string.IsNullOrEmpty(att.GivenName) == false)
            {
                requestBody.GivenName = att.GivenName;
                count++;
            }

            // Check the surname and set it to the request body
            if (att.DontSkipEmptyString || string.IsNullOrEmpty(att.Surname) == false)
            {
                requestBody.Surname = att.Surname;
                count++;
            }

            // Check the country and set it to the request body
            if (att.DontSkipEmptyString || string.IsNullOrEmpty(att.Country) == false)
            {
                requestBody.Country = att.Country;
                count++;
            }

            // Check the city and set it to the request body
            if (att.DontSkipEmptyString || string.IsNullOrEmpty(att.City) == false)
            {
                requestBody.City = att.City;
                count++;
            }

            // Check if there are any attributes to be updated
            if (count == 0)
            {
                att.ErrorMessage = "No attributes were provided to be updated.";
                return Ok(att);
            }

            // There is an issue with the delegated permissions, thefore we comment the next line and use app permissions
            //var result = await _graphServiceClient.Me.PatchAsync(requestBody);

            // Aquire the access token to call the Graph API with app permissions
            var graphClient = MsalAccessTokenHandler.GetGraphClient(_configuration);

            // Call the Graph API to update the user profile using the user object ID
            User? result = await graphClient.Users[userObjectId].PatchAsync(requestBody);
        }
        catch (ODataError odataError)
        {
            att.ErrorMessage = $"The account cannot be updated due to the following error: {odataError.Error!.Message} Error code: {odataError.Error.Code}";
            //TrackException(odataError, "OnPostProfileAsync");
        }
        catch (Exception ex)
        {
            string error = ex.InnerException == null ? ex.Message : ex.InnerException.Message;
            att.ErrorMessage = $"The account cannot be updated due to the following error: {error}";
            //TrackException(ex, "OnPostProfileAsync");
        }

        return Ok(att);
    }
}
