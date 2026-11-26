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

using CloudStreams.Core.Resources;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Transports.Authentication;

/// <summary>
/// Represents a request for an OAuth2 token
/// </summary>
public sealed record OAuth2TokenRequest
{

    /// <summary>
    /// Gets a boolean indicating whether the token endpoint is discovered from the authority's OpenID Connect configuration
    /// </summary>
    public bool Discover { get; init; }

    /// <summary>
    /// Gets the address of the authorization server
    /// </summary>
    public Uri Authority { get; init; } = null!;

    /// <summary>
    /// Gets the path, relative to the authority, or the address of the token endpoint, if not discovered. Defaults to <c>/oauth2/token</c>
    /// </summary>
    public string? TokenEndpoint { get; init; }

    /// <summary>
    /// Gets the grant. See <see cref="OAuth2GrantType"/>
    /// </summary>
    public string Grant { get; init; } = OAuth2GrantType.ClientCredentials;

    /// <summary>
    /// Gets the id of the client
    /// </summary>
    public string ClientId { get; init; } = null!;

    /// <summary>
    /// Gets the secret of the client, if any
    /// </summary>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Gets how the client authenticates. See <see cref="OAuth2ClientAuthenticationMethod"/>
    /// </summary>
    public string ClientAuthentication { get; init; } = OAuth2ClientAuthenticationMethod.ClientSecretPost;

    /// <summary>
    /// Gets the scopes to request, if any
    /// </summary>
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>
    /// Gets the audiences to request, if any
    /// </summary>
    public IReadOnlyList<string>? Audiences { get; init; }

    /// <summary>
    /// Gets the user name, with the password grant
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Gets the password, with the password grant
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Gets a key that identifies the request, to cache its tokens
    /// </summary>
    public string Key => string.Join('\u001f', this.Discover, this.Authority, this.TokenEndpoint, this.Grant, this.ClientId, this.ClientSecret, this.ClientAuthentication,
        string.Join(' ', this.Scopes ?? []), string.Join(' ', this.Audiences ?? []), this.Username, this.Password);

}

/// <summary>
/// Defines the fundamentals of the service used to obtain OAuth2 and OpenID Connect tokens
/// </summary>
public interface IOAuth2TokenClient
{

    /// <summary>
    /// Gets a token for the specified request, from the cache if it has not expired yet
    /// </summary>
    /// <param name="request">The request to get a token for</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The token</returns>
    /// <exception cref="InvalidOperationException">The authorization server refused to issue a token, or could not be reached</exception>
    Task<AccessToken> GetTokenAsync(OAuth2TokenRequest request, CancellationToken cancellationToken = default);

}

/// <summary>
/// Represents the default implementation of the <see cref="IOAuth2TokenClient"/>, which caches tokens until shortly before they expire
/// </summary>
/// <param name="httpClientFactory">The service used to create <see cref="HttpClient"/>s</param>
/// <param name="timeProvider">The service used to get the current time</param>
public class OAuth2TokenClient(IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
    : IOAuth2TokenClient
{

    /// <summary>
    /// Gets the name of the <see cref="HttpClient"/> the client uses
    /// </summary>
    public const string HttpClientName = "cloud-streams-oauth2";

    static readonly TimeSpan RenewalMargin = TimeSpan.FromSeconds(30);

    readonly ConcurrentDictionary<string, AccessToken> _tokens = new();
    readonly ConcurrentDictionary<string, Uri> _tokenEndpoints = new();
    readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    /// <inheritdoc/>
    public virtual async Task<AccessToken> GetTokenAsync(OAuth2TokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = request.Key;
        if (this._tokens.TryGetValue(key, out var cached) && cached.ExpiresAt - RenewalMargin > timeProvider.GetUtcNow()) return cached;
        var semaphore = this._locks.GetOrAdd(key, _ => new(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this._tokens.TryGetValue(key, out cached) && cached.ExpiresAt - RenewalMargin > timeProvider.GetUtcNow()) return cached;
            var token = await this.RequestTokenAsync(request, cancellationToken).ConfigureAwait(false);
            this._tokens[key] = token;
            return token;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Requests a new token from the authorization server
    /// </summary>
    protected virtual async Task<AccessToken> RequestTokenAsync(OAuth2TokenRequest request, CancellationToken cancellationToken)
    {
        var endpoint = await this.GetTokenEndpointAsync(request, cancellationToken).ConfigureAwait(false);
        var form = new List<KeyValuePair<string, string>>() { new("grant_type", request.Grant) };
        if (request.Grant == OAuth2GrantType.Password)
        {
            form.Add(new("username", request.Username ?? throw new InvalidOperationException("The password grant requires a user name")));
            form.Add(new("password", request.Password ?? throw new InvalidOperationException("The password grant requires a password")));
        }
        if (request.Scopes?.Count > 0) form.Add(new("scope", string.Join(' ', request.Scopes)));
        foreach (var audience in request.Audiences ?? []) form.Add(new("audience", audience));
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (request.ClientAuthentication == OAuth2ClientAuthenticationMethod.ClientSecretBasic)
        {
            var credentials = $"{Uri.EscapeDataString(request.ClientId)}:{Uri.EscapeDataString(request.ClientSecret ?? string.Empty)}";
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
        }
        else
        {
            form.Add(new("client_id", request.ClientId));
            if (request.ClientSecret != null) form.Add(new("client_secret", request.ClientSecret));
        }
        message.Content = new FormUrlEncodedContent(form);
        using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(message, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        JsonNode? body = null;
        try { body = JsonNode.Parse(content); }
        catch (System.Text.Json.JsonException) { }
        if (!response.IsSuccessStatusCode)
        {
            var error = body?["error_description"]?.GetValue<string>() ?? body?["error"]?.GetValue<string>() ?? content;
            throw new InvalidOperationException($"The authorization server '{endpoint}' did not issue a token: '{(int)response.StatusCode} {response.StatusCode}'{(string.IsNullOrWhiteSpace(error) ? string.Empty : $": {error}")}");
        }
        var accessToken = body?["access_token"]?.GetValue<string>() ?? throw new InvalidOperationException($"The response of the authorization server '{endpoint}' has no access token");
        var expiresIn = body?["expires_in"] is JsonValue value && value.TryGetValue<long>(out var seconds) ? seconds : 300;
        return new(accessToken, timeProvider.GetUtcNow().AddSeconds(expiresIn));
    }

    /// <summary>
    /// Gets the address of the token endpoint of the specified request, discovering it from the authority's OpenID Connect configuration if needed
    /// </summary>
    protected virtual async Task<Uri> GetTokenEndpointAsync(OAuth2TokenRequest request, CancellationToken cancellationToken)
    {
        var authority = request.Authority.OriginalString.EndsWith('/') ? request.Authority : new Uri(request.Authority.OriginalString + "/");
        if (!request.Discover) return Uri.TryCreate(request.TokenEndpoint, UriKind.Absolute, out var absolute) ? absolute : new Uri(authority, (request.TokenEndpoint ?? OAuth2Endpoints.DefaultToken).TrimStart('/'));
        if (this._tokenEndpoints.TryGetValue(authority.OriginalString, out var discovered)) return discovered;
        var configurationUri = new Uri(authority, ".well-known/openid-configuration");
        using var response = await httpClientFactory.CreateClient(HttpClientName).GetAsync(configurationUri, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Failed to discover the OpenID Connect configuration of '{authority}': '{configurationUri}' responded with '{(int)response.StatusCode} {response.StatusCode}'");
        var configuration = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var endpoint = configuration?["token_endpoint"]?.GetValue<string>() ?? throw new InvalidOperationException($"The OpenID Connect configuration of '{authority}' has no token endpoint");
        discovered = new Uri(endpoint);
        this._tokenEndpoints[authority.OriginalString] = discovered;
        return discovered;
    }

}
