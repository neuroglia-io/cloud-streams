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

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the options used to configure an <see cref="ApiKeyAuthenticationHandler"/>
/// </summary>
public class ApiKeyAuthenticationOptions
    : AuthenticationSchemeOptions
{

    /// <summary>
    /// Gets/sets the name of the HTTP header used to convey the API key
    /// </summary>
    public virtual string HeaderName { get; set; } = Configuration.IngestionAuthenticationOptions.DefaultApiKeyHeader;

    /// <summary>
    /// Gets/sets the accepted API keys
    /// </summary>
    public virtual List<string> ApiKeys { get; set; } = [];

}

/// <summary>
/// Represents the <see cref="AuthenticationHandler{TOptions}"/> used to authenticate requests by means of a static API key
/// </summary>
/// <inheritdoc/>
public class ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{

    /// <summary>
    /// Gets the name of the API key authentication scheme
    /// </summary>
    public const string AuthenticationScheme = "ApiKey";

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!this.Request.Headers.TryGetValue(this.Options.HeaderName, out var values) || string.IsNullOrWhiteSpace(values.ToString())) return Task.FromResult(AuthenticateResult.NoResult());
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(values.ToString()));
        var matched = false;
        // every key is compared, in fixed time, so that the response time reveals neither which key nor how much of it matched
        foreach (var key in this.Options.ApiKeys.Where(k => !string.IsNullOrWhiteSpace(k))) matched |= CryptographicOperations.FixedTimeEquals(providedHash, SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        if (!matched) return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "api-key")], AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), AuthenticationScheme)));
    }

    /// <inheritdoc/>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        this.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        return Task.CompletedTask;
    }

}
