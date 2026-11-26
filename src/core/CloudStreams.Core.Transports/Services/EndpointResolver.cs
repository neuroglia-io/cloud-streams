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
using Microsoft.Extensions.DependencyInjection;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using System.Collections.Concurrent;

namespace CloudStreams.Core.Transports.Services;

/// <summary>
/// Defines the fundamentals of the service that resolves the endpoints of <see cref="Sink"/>s and <see cref="Feed"/>s, which resources refer to by name (see #218)
/// </summary>
public interface IEndpointResolver
{

    /// <summary>
    /// Gets the endpoint of the specified sink
    /// </summary>
    /// <param name="sink">The name of the sink</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The endpoint of the sink</returns>
    /// <exception cref="InvalidOperationException">The sink does not exist</exception>
    Task<Endpoint> GetSinkEndpointAsync(string sink, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the endpoint of the specified feed
    /// </summary>
    /// <param name="feed">The reference to the feed: the name of a feed, or a feed of its own</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The name of the feed and its endpoint</returns>
    /// <exception cref="InvalidOperationException">The feed does not exist</exception>
    Task<(string Name, Endpoint Endpoint)> GetFeedAsync(FeedReference feed, CancellationToken cancellationToken = default);

}

/// <summary>
/// Represents the default implementation of the <see cref="IEndpointResolver"/>, which reads sinks and feeds from the resource repository, and caches them briefly
/// </summary>
/// <param name="serviceProvider">The current <see cref="IServiceProvider"/>, which provides the resource repository</param>
/// <param name="timeProvider">The service used to get the current time</param>
/// <remarks>Changes to a sink or a feed are picked up within the cache's duration, a few seconds</remarks>
public class EndpointResolver(IServiceProvider serviceProvider, TimeProvider timeProvider)
    : IEndpointResolver
{

    static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    readonly ConcurrentDictionary<string, (Endpoint Endpoint, DateTimeOffset ExpiresAt)> _cache = new();

    /// <inheritdoc/>
    public virtual Task<Endpoint> GetSinkEndpointAsync(string sink, CancellationToken cancellationToken = default) => this.GetEndpointAsync<Sink>(sink, s => s.Spec?.Endpoint, cancellationToken);

    /// <inheritdoc/>
    public virtual async Task<(string Name, Endpoint Endpoint)> GetFeedAsync(FeedReference feed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feed);
        if (feed.Feed == null) return (feed.Name ?? throw new InvalidOperationException("A feed of a gateway's own must have a name"), feed.Endpoint ?? throw new InvalidOperationException($"The feed '{feed.Name}' has no endpoint"));
        return (feed.Feed, await this.GetEndpointAsync<Feed>(feed.Feed, f => f.Spec?.Endpoint, cancellationToken).ConfigureAwait(false));
    }

    async Task<Endpoint> GetEndpointAsync<TResource>(string name, Func<TResource, Endpoint?> getEndpoint, CancellationToken cancellationToken)
        where TResource : class, IResource, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var kind = new TResource().Definition.Kind.ToLowerInvariant();
        var key = $"{kind}/{name}";
        var now = timeProvider.GetUtcNow();
        if (this._cache.TryGetValue(key, out var cached) && cached.ExpiresAt > now) return cached.Endpoint;
        var repository = serviceProvider.GetService<IResourceRepository>() ?? throw new InvalidOperationException($"The {kind} '{name}' cannot be resolved without a resource repository");
        var resource = await repository.GetAsync<TResource>(name, null, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException($"The {kind} '{name}' does not exist");
        var endpoint = getEndpoint(resource) ?? throw new InvalidOperationException($"The {kind} '{name}' has no endpoint");
        this._cache[key] = (endpoint, now + CacheDuration);
        return endpoint;
    }

}
