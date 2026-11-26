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
using CloudStreams.Core.Api.Configuration;
using CloudStreams.Core.Api.Services;
using CloudStreams.Core.Resources;
using CloudStreams.Core.Transports.Authentication;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CloudStreams.Gateway.Api.Services;

/// <summary>
/// Describes whether a request may publish cloud events to a gateway
/// </summary>
/// <param name="Status">The status code to answer a refused request with, or <c>200</c> if the request may publish</param>
/// <param name="Publisher">The name of the publisher, if it is known</param>
/// <param name="AllowedSources">The patterns of the sources the publisher may publish cloud events of, or null if it may publish cloud events of any source</param>
/// <param name="Reason">Why the request is refused, if it is</param>
public sealed record PublisherAuthorization(int Status, string? Publisher = null, IReadOnlyCollection<string>? AllowedSources = null, string? Reason = null)
{

    /// <summary>
    /// Gets a boolean indicating whether or not the request may publish
    /// </summary>
    public bool Allowed => this.Status == (int)HttpStatusCode.OK;

}

/// <summary>
/// Defines the fundamentals of the service that decides whether a request may publish cloud events to a gateway (see #215)
/// </summary>
public interface IPublisherAuthorizer
{

    /// <summary>
    /// Decides whether the specified request may publish cloud events to the specified gateway
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> of the request, whose user the cluster's ingestion authentication has authenticated, if it could</param>
    /// <param name="gateway">The gateway the request publishes to</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="PublisherAuthorization"/></returns>
    Task<PublisherAuthorization> AuthorizeAsync(HttpContext httpContext, Core.Resources.Gateway gateway, CancellationToken cancellationToken = default);

}

/// <summary>
/// Represents the default implementation of the <see cref="IPublisherAuthorizer"/>
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="authenticationOptions">The options used to configure authentication</param>
/// <param name="credentials">The service used to read the API keys of the gateways' publishers from their secrets</param>
/// <remarks>
/// <para>
/// A gateway that lists publishers only admits them, and the publishers the cluster's ingestion authentication allows to publish to its namespace. Its publishers are
/// identified by an API key, read from a secret of the gateway's namespace, or by the subject and claims of a bearer token issued by the authority Cloud Streams
/// trusts.
/// </para>
/// <para>
/// A gateway that lists none admits whoever the cluster's ingestion authentication admits: anyone if it is disabled, as before namespaces existed, and otherwise the
/// publishers allowed to publish to the gateway's namespace.
/// </para>
/// </remarks>
public class PublisherAuthorizer(ILogger<PublisherAuthorizer> logger, CloudStreamsAuthenticationOptions authenticationOptions, ICredentialResolver credentials)
    : IPublisherAuthorizer
{

    /// <summary>
    /// Gets the name of the claim that holds the subject of a bearer token
    /// </summary>
    public const string SubjectClaimType = "sub";

    /// <inheritdoc/>
    public virtual async Task<PublisherAuthorization> AuthorizeAsync(HttpContext httpContext, Core.Resources.Gateway gateway, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(gateway);
        var ingestion = authenticationOptions.Ingestion;
        var @namespace = gateway.GetNamespace() ?? Namespace.DefaultNamespaceName;
        var user = httpContext.User;
        var authenticated = user.Identities.Any(i => i.IsAuthenticated);
        var publishers = gateway.Spec.Ingestion?.Publishers;
        if (publishers is { Count: > 0 })
        {
            var presentedKey = false;
            foreach (var publisher in publishers.Where(p => p.ApiKey != null))
            {
                var header = string.IsNullOrWhiteSpace(publisher.ApiKey!.Header) ? ApiKeyAuthenticationPolicy.DefaultHeader : publisher.ApiKey.Header;
                if (!httpContext.Request.Headers.TryGetValue(header, out var values) || string.IsNullOrWhiteSpace(values.ToString())) continue;
                presentedKey = true;
                var key = await this.GetKeyAsync(gateway, publisher, cancellationToken).ConfigureAwait(false);
                if (key != null && FixedTimeEquals(values.ToString(), key)) return Allow(publisher);
            }
            foreach (var publisher in publishers.Where(p => p.UsesToken))
            {
                if (user.Identities.Any(i => i.IsAuthenticated && i.AuthenticationType != ApiKeyAuthenticationHandler.AuthenticationScheme && Matches(i, publisher))) return Allow(publisher);
            }
            if (ingestion.Enabled && authenticated && IsAllowedIn(user, @namespace, ingestion)) return new((int)HttpStatusCode.OK, GetName(user), user.GetAllowedSources(ingestion));
            return authenticated || presentedKey
                ? new((int)HttpStatusCode.Forbidden, Reason: $"The publisher is not allowed to publish to the gateway '{gateway.GetName()}' of the namespace '{@namespace}'")
                : new((int)HttpStatusCode.Unauthorized, Reason: $"The gateway '{gateway.GetName()}' of the namespace '{@namespace}' only admits its publishers");
        }
        if (!ingestion.Enabled) return new((int)HttpStatusCode.OK, authenticated ? GetName(user) : null, null);
        if (!authenticated) return new((int)HttpStatusCode.Unauthorized, Reason: "Publishing cloud events requires authentication");
        if (!IsAllowedIn(user, @namespace, ingestion)) return new((int)HttpStatusCode.Forbidden, Reason: $"The publisher is not allowed to publish to the namespace '{@namespace}'");
        return new((int)HttpStatusCode.OK, GetName(user), user.GetAllowedSources(ingestion));
    }

    /// <summary>
    /// Gets the API key of the specified publisher
    /// </summary>
    /// <param name="gateway">The gateway the publisher belongs to</param>
    /// <param name="publisher">The publisher to get the API key of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The API key of the publisher, or null if it cannot be read</returns>
    protected virtual async Task<string?> GetKeyAsync(Core.Resources.Gateway gateway, IngestionPublisher publisher, CancellationToken cancellationToken)
    {
        try
        {
            // the key is read from the secrets of the gateway's namespace, which the secret store caches briefly
            var resolved = await credentials.ResolveAsync(new AuthenticationPolicy() { ApiKey = publisher.ApiKey }, gateway.GetNamespace(), cancellationToken).ConfigureAwait(false);
            return resolved?.ApiKey;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Cannot read the API key of the publisher '{publisher}' of the gateway '{gateway}': {reason}", publisher.Name, gateway.GetQualifiedName(), ex.Message);
            return null;
        }
    }

    static PublisherAuthorization Allow(IngestionPublisher publisher) => new((int)HttpStatusCode.OK, publisher.Name, publisher.Sources is { Count: > 0 } sources ? sources : null);

    static bool Matches(ClaimsIdentity identity, IngestionPublisher publisher)
    {
        if (!string.IsNullOrWhiteSpace(publisher.Subject) && identity.FindFirst(SubjectClaimType)?.Value != publisher.Subject) return false;
        // a claim holding an array is mapped to one claim per value: a token has a claim if any of its values matches
        return (publisher.Claims ?? new Dictionary<string, string>()).All(c => identity.FindAll(c.Key).Any(claim => claim.Value == c.Value));
    }

    static bool IsAllowedIn(ClaimsPrincipal user, string @namespace, IngestionAuthenticationOptions ingestion)
    {
        var namespaces = user.GetAllowedNamespaces(ingestion);
        return namespaces == null || namespaces.Contains(@namespace);
    }

    static string? GetName(ClaimsPrincipal user) => user.Identities.FirstOrDefault(i => i.IsAuthenticated)?.Name;

    static bool FixedTimeEquals(string provided, string expected) => CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(provided)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

}
