// Copyright © 2024-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CloudStreams.Core.Api;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the independent authentication of the dashboard and of the ingestion endpoint requested by issue #130
/// </summary>
public class AuthenticationTests
{

    const string Authority = "https://idp.cloud-streams.test/realms/test";
    const string Audience = "cloud-streams";
    static readonly SymmetricSecurityKey SigningKey = new(RandomNumberGenerator.GetBytes(32));

    static async Task<(WebApplication App, HttpClient Client)> CreateAsync(Dictionary<string, string?> settings)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthenticationTests).Assembly);
        builder.Services.AddCloudStreamsAuthentication(builder.Configuration);
        // validate tokens against a local key rather than against the metadata of a real authority
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Configuration = new OpenIdConnectConfiguration() { Issuer = Authority };
            options.Configuration.SigningKeys.Add(SigningKey);
            options.ConfigurationManager = new Microsoft.IdentityModel.Protocols.StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
        });
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapCloudStreamsAuthenticationEndpoints();
        app.MapFallback(() => "dashboard").RequireAuthorization(CloudStreamsAuthenticationDefaults.DashboardPagePolicy);
        await app.StartAsync();
        var client = app.GetTestClient();
        return (app, client);
    }

    static string CreateToken(string audience = Audience, params string[] roles) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor()
    {
        Issuer = Authority,
        Audience = audience,
        Expires = DateTime.UtcNow.AddMinutes(5),
        Claims = new Dictionary<string, object>() { { "preferred_username", "jane" }, { "roles", roles } },
        SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
    });

    static Dictionary<string, string?> Settings(bool dashboard, bool ingestion, params string[] apiKeys)
    {
        var settings = new Dictionary<string, string?>()
        {
            ["Authentication:Oidc:Authority"] = Authority,
            ["Authentication:Oidc:ClientId"] = "cloud-streams-dashboard",
            ["Authentication:Oidc:Audience"] = Audience,
            ["Authentication:Dashboard:Enabled"] = dashboard.ToString(),
            ["Authentication:Ingestion:Enabled"] = ingestion.ToString()
        };
        for (var i = 0; i < apiKeys.Length; i++) settings[$"Authentication:Ingestion:ApiKeys:{i}"] = apiKeys[i];
        return settings;
    }

    [Fact]
    public async Task Everything_Should_Be_Anonymous_By_Default()
    {
        // arrange
        var (app, client) = await CreateAsync([]);
        await using var _ = app;

        // act & assert
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/test/resources")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("api/test/events/pub", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Dashboard_Authentication_Should_Protect_The_Api_And_Pages_But_Not_Ingestion()
    {
        // arrange
        var (app, client) = await CreateAsync(Settings(dashboard: true, ingestion: false));
        await using var _ = app;

        // act
        var api = await client.GetAsync("api/test/resources");
        var page = await client.GetAsync("/subscriptions");
        var ingestion = await client.PostAsync("api/test/events/pub", null);

        // assert - API calls get a plain 401, pages are redirected to the sign-in endpoint, and publishers are not affected
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.StartsWith("http://localhost/auth/login?returnUrl=", page.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, ingestion.StatusCode);
    }

    [Fact]
    public async Task Signing_Out_Should_Not_Be_Possible_With_A_Get_Request()
    {
        // arrange
        var (app, client) = await CreateAsync(Settings(dashboard: true, ingestion: false));
        await using var _ = app;

        // act - a link or an image on another site can only issue GET requests (see #150)
        var response = await client.GetAsync("auth/logout");

        // assert - the request is handled like any other page, which sends anonymous users to sign in, rather than signing anyone out
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("http://localhost/auth/login?returnUrl=", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Dashboard_Api_Should_Accept_Bearer_Tokens_Issued_For_The_Configured_Audience()
    {
        // arrange
        var (app, client) = await CreateAsync(Settings(dashboard: true, ingestion: false));
        await using var _ = app;

        // act
        using var valid = new HttpRequestMessage(HttpMethod.Get, "api/test/resources") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", CreateToken()) } };
        using var wrongAudience = new HttpRequestMessage(HttpMethod.Get, "api/test/resources") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("another-api")) } };
        var validResponse = await client.SendAsync(valid);
        var wrongAudienceResponse = await client.SendAsync(wrongAudience);

        // assert
        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
        Assert.Equal("jane", await validResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, wrongAudienceResponse.StatusCode);
    }

    [Fact]
    public async Task Required_Roles_Should_Be_Enforced()
    {
        // arrange
        var settings = Settings(dashboard: true, ingestion: false);
        settings["Authentication:Dashboard:RequiredRoles:0"] = "cloud-streams-admin";
        var (app, client) = await CreateAsync(settings);
        await using var _ = app;

        // act
        using var withoutRole = new HttpRequestMessage(HttpMethod.Get, "api/test/resources") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(Audience, "viewer")) } };
        using var withRole = new HttpRequestMessage(HttpMethod.Get, "api/test/resources") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(Audience, "cloud-streams-admin")) } };

        // assert
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(withoutRole)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(withRole)).StatusCode);
    }

    [Fact]
    public async Task Ingestion_Authentication_Should_Accept_Api_Keys_And_Bearer_Tokens_Independently_Of_The_Dashboard()
    {
        // arrange
        var (app, client) = await CreateAsync(Settings(dashboard: false, ingestion: true, "s3cr3t"));
        await using var _ = app;

        // act
        var anonymous = await client.PostAsync("api/test/events/pub", null);
        using var wrongKey = new HttpRequestMessage(HttpMethod.Post, "api/test/events/pub") { Headers = { { "X-Api-Key", "nope" } } };
        using var rightKey = new HttpRequestMessage(HttpMethod.Post, "api/test/events/pub") { Headers = { { "X-Api-Key", "s3cr3t" } } };
        using var bearer = new HttpRequestMessage(HttpMethod.Post, "api/test/events/pub") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", CreateToken()) } };

        // assert
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(wrongKey)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(rightKey)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(bearer)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/test/resources")).StatusCode);
    }

    [Fact]
    public async Task User_Endpoint_Should_Describe_The_Current_User()
    {
        // arrange
        var (app, client) = await CreateAsync(Settings(dashboard: true, ingestion: false));
        await using var _ = app;

        // act
        var json = await client.GetStringAsync("api/auth/user");

        // assert
        Assert.Contains("\"enabled\":true", json);
        Assert.Contains("\"authenticated\":false", json);
    }

    [Fact]
    public void Invalid_Configuration_Should_Fail_Fast()
    {
        // arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>() { ["Authentication:Dashboard:Enabled"] = "true" }).Build();

        // act
        var exception = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCloudStreamsAuthentication(configuration));

        // assert
        Assert.Contains("Authentication:Oidc:Authority", exception.Message);
    }

}

/// <summary>
/// Stands for the management API, protected by the dashboard policy by convention
/// </summary>
[Route("api/test/resources")]
public class TestResourcesController
    : ControllerBase
{

    [HttpGet]
    public IActionResult Get() => this.Ok(this.User.Identity?.Name ?? "anonymous");

}

/// <summary>
/// Stands for the cloud event ingestion endpoint, which declares its own policy
/// </summary>
[Route("api/test/events")]
public class TestEventsController
    : ControllerBase
{

    [HttpPost("pub")]
    [Authorize(Policy = CloudStreamsAuthenticationDefaults.IngestionPolicy)]
    public IActionResult Publish() => this.Ok();

}
