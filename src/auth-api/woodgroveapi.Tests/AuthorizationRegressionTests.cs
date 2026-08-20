using Xunit;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using woodgroveapi.Models;

namespace woodgroveapi.Tests;

public class AuthorizationRegressionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthorizationRegressionTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Theory]
    [InlineData("/OnTokenIssuanceStart")]
    [InlineData("/OnAttributeCollectionStart")]
    [InlineData("/OnAttributeCollectionSubmit")]
    [InlineData("/onPageRenderStart")]
    public async Task ProtectedEndpoints_RequireBearerToken(string path)
    {
        using var response = await _client.PostAsJsonAsync(path, CreatePayload(path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static object CreatePayload(string path) =>
        path switch
        {
            "/OnTokenIssuanceStart" => new TokenIssuanceStartRequest
            {
                data = CreateBaseData<TokenIssuanceStartRequest_Data>(data =>
                {
                    data.authenticationContext.user = new AuthenticationContext_User { id = "user-1" };
                })
            },
            "/OnAttributeCollectionStart" => new AttributeCollectionRequest
            {
                data = CreateBaseData<AttributeCollectionRequest_Data>(data =>
                {
                    data.userSignUpInfo = CreateUserSignUpInfo();
                })
            },
            "/OnAttributeCollectionSubmit" => new AttributeCollectionRequest
            {
                data = CreateBaseData<AttributeCollectionRequest_Data>(data =>
                {
                    data.userSignUpInfo = CreateUserSignUpInfo(
                        city: "Seattle",
                        country: "us",
                        displayName: "Shopper");
                })
            },
            "/onPageRenderStart" => new PageRenderStartRequest
            {
                type = "microsoft.graph.onPageRenderStart",
                source = "unit-test",
                data = CreateBaseData<PageRenderStartRequest_Data>(data =>
                {
                    data.pageId = "signup";
                })
            },
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
        };

    private static TRequestData CreateBaseData<TRequestData>(Action<TRequestData>? configure = null)
        where TRequestData : AllRequestData, new()
    {
        var data = new TRequestData
        {
            odatatype = "#microsoft.graph.onTokenIssuanceStartCalloutData",
            tenantId = "tenant-id",
            authenticationEventListenerId = "listener-id",
            customAuthenticationExtensionId = "extension-id",
            authenticationContext = new AuthenticationContext
            {
                correlationId = "correlation-id",
                protocol = "OIDC",
                client = new AuthenticationContext_Client
                {
                    ip = "127.0.0.1",
                    locale = "en-US",
                    market = "US"
                },
                clientServicePrincipal = new AuthenticationContext_ServicePrincipal
                {
                    id = "client-sp-id",
                    appId = "7a30b8ed-42a3-4d1e-89ad-14d4ca3c9a52",
                    appDisplayName = "Woodgrove client",
                    displayName = "Woodgrove client"
                },
                resourceServicePrincipal = new AuthenticationContext_ServicePrincipal
                {
                    id = "resource-sp-id",
                    appId = "resource-app-id",
                    appDisplayName = "Woodgrove resource",
                    displayName = "Woodgrove resource"
                },
                user = new AuthenticationContext_User
                {
                    id = "user-1",
                    displayName = "Shopper"
                }
            }
        };

        configure?.Invoke(data);
        return data;
    }

    private static UserSignUpInfo CreateUserSignUpInfo(
        string city = "Madrid",
        string country = "es",
        string displayName = "Shopper")
    {
        return new UserSignUpInfo
        {
            attributes = new UserSignUpInfo_Attributes
            {
                city = new UserSignUpInfo_Attribute
                {
                    value = city,
                    odatatype = "String",
                    attributeType = "builtIn"
                },
                country = new UserSignUpInfo_Attribute
                {
                    value = country,
                    odatatype = "String",
                    attributeType = "builtIn"
                },
                displayName = new UserSignUpInfo_Attribute
                {
                    value = displayName,
                    odatatype = "String",
                    attributeType = "builtIn"
                }
            },
            identities =
            [
                new UserSignUpInfo_Identities
                {
                    signInType = "emailAddress",
                    issuer = "woodgrovegroceries.onmicrosoft.com",
                    issuerAssignedId = "shopper@example.com"
                }
            ]
        };
    }
}
