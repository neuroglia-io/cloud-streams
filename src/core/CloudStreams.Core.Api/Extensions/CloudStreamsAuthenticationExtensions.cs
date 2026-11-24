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

using CloudStreams.Core.Api.Configuration;
using CloudStreams.Core.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CloudStreams.Core.Api;

/// <summary>
/// Exposes constants used to configure the authentication and authorization of Cloud Streams APIs
/// </summary>
public static class CloudStreamsAuthenticationDefaults
{

    /// <summary>
    /// Gets the name of the policy that protects the management API, the hubs and the MCP endpoint used by the dashboard
    /// </summary>
    public const string DashboardPolicy = "cloud-streams:dashboard";

    /// <summary>
    /// Gets the name of the policy that protects the dashboard's pages, which, unlike <see cref="DashboardPolicy"/>, redirects anonymous users to the sign-in page
    /// </summary>
    public const string DashboardPagePolicy = "cloud-streams:dashboard-page";

    /// <summary>
    /// Gets the name of the policy that protects the cloud event ingestion endpoint
    /// </summary>
    public const string IngestionPolicy = "cloud-streams:ingestion";

    /// <summary>
    /// Gets the name of the cookie used to authenticate the dashboard's users
    /// </summary>
    public const string CookieName = "cloud-streams.auth";

}

/// <summary>
/// Defines extensions used to configure the authentication and authorization of Cloud Streams APIs
/// </summary>
public static class CloudStreamsAuthenticationExtensions
{

    const string DataProtectionKey = "cloud-streams:data-protection-keys";

    /// <summary>
    /// Adds and configures the authentication and authorization of Cloud Streams APIs, as configured by the <see cref="CloudStreamsAuthenticationOptions"/>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="configuration">The current <see cref="IConfiguration"/></param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    /// <exception cref="InvalidOperationException">The <see cref="CloudStreamsAuthenticationOptions"/> are invalid</exception>
    public static IServiceCollection AddCloudStreamsAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(CloudStreamsAuthenticationOptions.ConfigurationSection).Get<CloudStreamsAuthenticationOptions>() ?? new();
        var errors = options.Validate().ToList();
        if (errors.Count > 0) throw new InvalidOperationException($"Invalid authentication configuration:{Environment.NewLine}{string.Join(Environment.NewLine, errors.Select(e => $"- {e}"))}");
        services.AddSingleton(options);
        services.ConfigureForwardedHeaders(configuration);
        services.Configure<MvcOptions>(mvc => mvc.Conventions.Add(new DashboardAuthorizationConvention()));

        var bearerEnabled = !string.IsNullOrWhiteSpace(options.Oidc.Authority);
        var apiKeys = options.Ingestion.ApiKeys?.Where(k => !string.IsNullOrWhiteSpace(k)).ToList() ?? [];
        var apiKeyEnabled = options.Ingestion.Enabled && apiKeys.Count > 0;
        var authentication = services.AddAuthentication(authenticationOptions =>
        {
            if (options.Dashboard.Enabled) authenticationOptions.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            else if (bearerEnabled) authenticationOptions.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            else if (apiKeyEnabled) authenticationOptions.DefaultScheme = ApiKeyAuthenticationHandler.AuthenticationScheme;
        });
        if (bearerEnabled)
        {
            authentication.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwt =>
            {
                jwt.Authority = options.Oidc.Authority;
                jwt.Audience = options.Oidc.Audience;
                jwt.RequireHttpsMetadata = options.Oidc.RequireHttpsMetadata;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.ValidateAudience = !string.IsNullOrWhiteSpace(options.Oidc.Audience);
                jwt.TokenValidationParameters.NameClaimType = options.Oidc.NameClaimType;
                jwt.TokenValidationParameters.RoleClaimType = options.Oidc.RoleClaimType;
                jwt.Events = new()
                {
                    // browsers cannot set headers on web socket requests, so SignalR conveys the token in the query string
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrWhiteSpace(accessToken) && context.Request.Path.Value?.Contains("/ws/", StringComparison.OrdinalIgnoreCase) == true) context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });
        }
        if (options.Dashboard.Enabled)
        {
            authentication
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, cookie =>
                {
                    cookie.Cookie.Name = CloudStreamsAuthenticationDefaults.CookieName;
                    cookie.Cookie.HttpOnly = true;
                    cookie.Cookie.SameSite = SameSiteMode.Lax;
                    cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    cookie.SlidingExpiration = true;
                    // page navigations are sent to the sign-in endpoint, which challenges the identity provider; API, hub and MCP calls get a plain 401/403 instead,
                    // which they can act upon, and which never depends on the identity provider being reachable
                    cookie.LoginPath = "/auth/login";
                    cookie.ReturnUrlParameter = "returnUrl";
                    cookie.Events = new()
                    {
                        OnRedirectToLogin = context =>
                        {
                            if (IsApiRequest(context.Request)) context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            else context.Response.Redirect(context.RedirectUri);
                            return Task.CompletedTask;
                        },
                        OnRedirectToAccessDenied = context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        }
                    };
                })
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, oidc =>
                {
                    oidc.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    oidc.Authority = options.Oidc.Authority;
                    oidc.ClientId = options.Oidc.ClientId;
                    oidc.ClientSecret = options.Oidc.ClientSecret;
                    oidc.RequireHttpsMetadata = options.Oidc.RequireHttpsMetadata;
                    oidc.ResponseType = "code";
                    oidc.UsePkce = true;
                    oidc.SaveTokens = false;
                    oidc.MapInboundClaims = false;
                    oidc.GetClaimsFromUserInfoEndpoint = true;
                    oidc.Scope.Clear();
                    foreach (var scope in options.Oidc.Scopes.Count > 0 ? options.Oidc.Scopes : ["openid", "profile"]) oidc.Scope.Add(scope);
                    if (!oidc.Scope.Contains("openid")) oidc.Scope.Add("openid");
                    oidc.TokenValidationParameters.NameClaimType = options.Oidc.NameClaimType;
                    oidc.TokenValidationParameters.RoleClaimType = options.Oidc.RoleClaimType;
                });
            // the cookie is encrypted with data protection keys, which must be shared by all replicas: they are persisted to the Redis database Cloud Streams already uses
            services.AddDataProtection().SetApplicationName("cloud-streams");
            services.TryAddSingleton<IConnectionMultiplexer>(provider => ConnectionMultiplexer.Connect(provider.GetRequiredService<IConfiguration>().GetConnectionString("redis")!));
            services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(provider => new ConfigureOptions<KeyManagementOptions>(keyManagement =>
                keyManagement.XmlRepository = new RedisXmlRepository(() => provider.GetRequiredService<IConnectionMultiplexer>().GetDatabase(), DataProtectionKey)));
        }
        if (apiKeyEnabled)
        {
            authentication.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.AuthenticationScheme, apiKey =>
            {
                apiKey.HeaderName = string.IsNullOrWhiteSpace(options.Ingestion.ApiKeyHeader) ? IngestionAuthenticationOptions.DefaultApiKeyHeader : options.Ingestion.ApiKeyHeader;
                apiKey.ApiKeys = apiKeys;
            });
        }

        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy(CloudStreamsAuthenticationDefaults.DashboardPolicy, policy =>
            {
                if (!options.Dashboard.Enabled)
                {
                    policy.RequireAssertion(_ => true);
                    return;
                }
                policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
                if (bearerEnabled) policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                if (options.Dashboard.RequiredRoles.Count > 0) policy.RequireRole(options.Dashboard.RequiredRoles);
            });
            authorization.AddPolicy(CloudStreamsAuthenticationDefaults.DashboardPagePolicy, policy =>
            {
                if (!options.Dashboard.Enabled)
                {
                    policy.RequireAssertion(_ => true);
                    return;
                }
                policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                if (options.Dashboard.RequiredRoles.Count > 0) policy.RequireRole(options.Dashboard.RequiredRoles);
            });
            authorization.AddPolicy(CloudStreamsAuthenticationDefaults.IngestionPolicy, policy =>
            {
                if (!options.Ingestion.Enabled)
                {
                    policy.RequireAssertion(_ => true);
                    return;
                }
                if (bearerEnabled) policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                if (apiKeyEnabled) policy.AddAuthenticationSchemes(ApiKeyAuthenticationHandler.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
            });
        });
        return services;
    }

    /// <summary>
    /// Configures how the headers set by reverse proxies, such as ingresses that terminate TLS, are honoured
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="configuration">The current <see cref="IConfiguration"/></param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    /// <remarks>
    /// <para>
    /// Without them, an application behind a TLS-terminating proxy believes requests are plain HTTP: cookies are issued without the 'Secure' flag, and OIDC redirect
    /// URIs are built with 'http' (see #150). The 'X-Forwarded-For' and 'X-Forwarded-Proto' headers are honoured when sent by a trusted proxy: loopback by default,
    /// otherwise the ones listed in 'ForwardedHeaders:KnownProxies' (addresses) and 'ForwardedHeaders:KnownNetworks' (CIDR ranges, e.g. '10.0.0.0/8').
    /// </para>
    /// <para>
    /// 'ForwardedHeaders:TrustAllProxies' trusts every proxy. Only enable it when the application cannot be reached without going through a proxy.
    /// </para>
    /// </remarks>
    public static IServiceCollection ConfigureForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("ForwardedHeaders");
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in section.GetSection("KnownProxies").Get<string[]>() ?? []) options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
            foreach (var network in section.GetSection("KnownNetworks").Get<string[]>() ?? []) options.KnownNetworks.Add(Microsoft.AspNetCore.HttpOverrides.IPNetwork.Parse(network));
            if (section.GetValue<bool>("TrustAllProxies"))
            {
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            }
        });
        return services;
    }

    /// <summary>
    /// Maps the endpoints used to sign the dashboard's users in and out, and to describe the current user
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to configure</param>
    /// <returns>The configured <see cref="IEndpointRouteBuilder"/></returns>
    public static IEndpointRouteBuilder MapCloudStreamsAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<CloudStreamsAuthenticationOptions>();
        endpoints.MapGet("api/auth/user", (HttpContext context) => Results.Ok(new
        {
            enabled = options.Dashboard.Enabled,
            authenticated = context.User.Identity?.IsAuthenticated == true,
            name = context.User.Identity?.Name
        })).AllowAnonymous().ExcludeFromDescription();
        if (!options.Dashboard.Enabled) return endpoints;
        endpoints.MapGet("auth/login", (string? returnUrl) => Results.Challenge(new AuthenticationProperties { RedirectUri = ToLocalUrl(returnUrl) }, [OpenIdConnectDefaults.AuthenticationScheme]))
            .AllowAnonymous().ExcludeFromDescription();
        // a POST rather than a GET: the session cookie is 'SameSite=Lax', so other sites cannot sign users out by sending them a link or an image (see #150)
        endpoints.MapPost("auth/logout", () => Results.SignOut(new AuthenticationProperties { RedirectUri = "/" }, [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]))
            .AllowAnonymous().ExcludeFromDescription();
        return endpoints;
    }

    static string ToLocalUrl(string? url) => !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\") ? url : "/";

    static bool IsApiRequest(HttpRequest request) => request.Path.StartsWithSegments("/api") || request.Path.StartsWithSegments("/mcp");

    /// <summary>
    /// Represents the <see cref="IApplicationModelConvention"/> that protects every controller action with the <see cref="CloudStreamsAuthenticationDefaults.DashboardPolicy"/>,
    /// unless it explicitly declares its own authorization requirements, like the cloud event ingestion endpoint does
    /// </summary>
    sealed class DashboardAuthorizationConvention
        : IApplicationModelConvention
    {

        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                foreach (var action in controller.Actions)
                {
                    if (controller.Attributes.Concat(action.Attributes).Any(a => a is IAuthorizeData || a is IAllowAnonymous)) continue;
                    foreach (var selector in action.Selectors) selector.EndpointMetadata.Add(new AuthorizeAttribute(CloudStreamsAuthenticationDefaults.DashboardPolicy));
                }
            }
        }

    }

}
