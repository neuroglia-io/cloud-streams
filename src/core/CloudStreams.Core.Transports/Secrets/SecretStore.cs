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
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CloudStreams.Core.Transports.Secrets;

/// <summary>
/// Represents a secret: a set of values, by key, whose keys are matched regardless of case and separators, so that <c>client.id</c>, <c>client_id</c> and
/// <c>CLIENT_ID</c> are the same key, as environment variables and file names require
/// </summary>
/// <param name="values">The values of the secret, by key</param>
public sealed class Secret(IReadOnlyDictionary<string, string> values)
{

    readonly Dictionary<string, string> _values = values.GroupBy(v => Normalize(v.Key)).ToDictionary(g => g.Key, g => g.First().Value);

    /// <summary>
    /// Gets the keys of the secret, normalized
    /// </summary>
    public IReadOnlyCollection<string> Keys => this._values.Keys;

    /// <summary>
    /// Gets the value of the specified key, if any
    /// </summary>
    /// <param name="key">The key to get the value of</param>
    /// <returns>The value of the key, or null if the secret does not have it</returns>
    public string? Get(string key) => this._values.TryGetValue(Normalize(key), out var value) ? value : null;

    static string Normalize(string key) => new([.. key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);

}

/// <summary>
/// Defines the fundamentals of a provider of secrets
/// </summary>
public interface ISecretProvider
{

    /// <summary>
    /// Gets the name of the provider. See <see cref="SecretProviders"/>
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Reads the specified secret
    /// </summary>
    /// <param name="path">The name or the path of the secret, within the provider</param>
    /// <param name="namespace">The namespace the secret belongs to, or null to read a shared secret, which belongs to no namespace</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The secret, or null if the provider does not have it</returns>
    /// <exception cref="InvalidOperationException">The provider is not configured, cannot be reached, or refused to provide the secret</exception>
    /// <remarks>The secrets of a namespace and shared secrets are stored apart, so that a path can never designate a secret of another namespace (see #215)</remarks>
    Task<Secret?> GetAsync(string path, string? @namespace, CancellationToken cancellationToken = default);

}

/// <summary>
/// Defines the fundamentals of the service that resolves references to secrets
/// </summary>
public interface ISecretStore
{

    /// <summary>
    /// Gets the specified secret
    /// </summary>
    /// <param name="reference">The reference to the secret</param>
    /// <param name="namespace">The namespace of the resource that refers to the secret, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The secret</returns>
    /// <exception cref="InvalidOperationException">The secret does not exist, or its provider is not configured</exception>
    /// <remarks>
    /// The secret is looked for among the secrets of the namespace first, then among the shared secrets if the namespace may read them
    /// (see <see cref="SecretOptions.SharedWith"/>). Without a namespace, only shared secrets are read
    /// </remarks>
    Task<Secret> GetAsync(SecretReference reference, string? @namespace, CancellationToken cancellationToken = default);

}

/// <summary>
/// Represents the default implementation of the <see cref="ISecretStore"/>, which reads secrets from the configured providers and caches them briefly
/// </summary>
/// <param name="providers">The available providers</param>
/// <param name="options">The service used to access the current <see cref="SecretOptions"/></param>
/// <param name="timeProvider">The service used to get the current time</param>
public class SecretStore(IEnumerable<ISecretProvider> providers, IOptionsMonitor<SecretOptions> options, TimeProvider timeProvider)
    : ISecretStore
{

    readonly ConcurrentDictionary<string, (Secret Secret, DateTimeOffset ExpiresAt)> _cache = new();

    /// <inheritdoc/>
    public virtual async Task<Secret> GetAsync(SecretReference reference, string? @namespace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!SecretReference.IsValidPath(reference.Name)) throw new InvalidOperationException($"'{reference.Name}' is not a valid secret path: {SecretReference.PathRules}");
        if (string.IsNullOrWhiteSpace(@namespace)) @namespace = null;
        var key = $"{@namespace}|{reference}";
        var now = timeProvider.GetUtcNow();
        if (this._cache.TryGetValue(key, out var cached) && cached.ExpiresAt > now) return cached.Secret;
        var configuration = options.CurrentValue;
        var configured = configuration.GetProviders();
        IEnumerable<string> candidates;
        if (reference.Provider == null) candidates = configured;
        else if (!configured.Contains(reference.Provider)) throw new InvalidOperationException($"The secret '{reference}' cannot be read: the '{reference.Provider}' secret provider is not enabled. Enable it with '{SecretOptions.ConfigurationSection}:Providers'");
        else candidates = [reference.Provider];
        var resolved = candidates.Select(name => providers.FirstOrDefault(p => p.Name == name) ?? throw new InvalidOperationException($"The secret provider '{name}' is not supported: use one of {string.Join(", ", SecretProviders.AsEnumerable())}")).ToList();
        // the secrets of the namespace take precedence over shared ones, in every provider
        var scopes = new List<string?>(2);
        if (@namespace != null) scopes.Add(@namespace);
        if (configuration.IsSharedWith(@namespace)) scopes.Add(null);
        foreach (var scope in scopes)
        {
            foreach (var provider in resolved)
            {
                var secret = await provider.GetAsync(reference.Name, scope, cancellationToken).ConfigureAwait(false);
                if (secret == null) continue;
                this._cache[key] = (secret, now + configuration.CacheDuration);
                return secret;
            }
        }
        var where = @namespace == null ? "among the shared secrets" : scopes.Count > 1 ? $"among the secrets of the namespace '{@namespace}' and the shared secrets" : $"among the secrets of the namespace '{@namespace}'";
        throw new InvalidOperationException(reference.Provider == null
            ? $"The secret '{reference.Name}' does not exist {where}: none of the configured providers ({string.Join(", ", configured)}) has it"
            : $"The secret '{reference.Name}' does not exist {where} of the '{reference.Provider}' provider");
    }

}
