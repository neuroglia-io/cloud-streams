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
using System.Collections;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Transports.Secrets;

/// <summary>
/// Reads secrets from environment variables: the secrets of a namespace from <c>CLOUDSTREAMS_SECRETS__NAMESPACES__{NAMESPACE}__{NAME}__{KEY}</c>, e.g.
/// <c>CLOUDSTREAMS_SECRETS__NAMESPACES__ORDERS__WEBHOOK__PASSWORD</c>, and shared secrets from <c>CLOUDSTREAMS_SECRETS__{NAME}__{KEY}</c>
/// </summary>
/// <remarks>
/// Names and keys are matched regardless of case, and of dashes, dots and underscores, which environment variables cannot all contain. The dashes of namespaces
/// are written as underscores, and namespaces are matched exactly otherwise
/// </remarks>
public class EnvironmentSecretProvider
    : ISecretProvider
{

    /// <summary>
    /// Gets the prefix of the environment variables secrets are read from
    /// </summary>
    public const string Prefix = "CLOUDSTREAMS_SECRETS__";

    /// <inheritdoc/>
    public string Name => SecretProviders.Environment;

    /// <inheritdoc/>
    public virtual Task<Secret?> GetAsync(string path, string? @namespace, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var name = Normalize(path);
        var namespaces = $"{SecretOptions.NamespacesSegment}__";
        // a shared secret cannot be named after the segment that holds the secrets of namespaces
        if (@namespace == null && name == SecretOptions.NamespacesSegment) return Task.FromResult<Secret?>(null);
        var prefix = @namespace == null ? Prefix : $"{Prefix}{namespaces}{@namespace.Replace('-', '_')}__";
        var values = new Dictionary<string, string>();
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            var key = variable.Key.ToString()!;
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = key[prefix.Length..];
            if (@namespace == null && rest.StartsWith(namespaces, StringComparison.OrdinalIgnoreCase)) continue;
            var separator = rest.IndexOf("__", StringComparison.Ordinal);
            if (separator < 1 || Normalize(rest[..separator]) != name) continue;
            values[rest[(separator + 2)..]] = variable.Value?.ToString() ?? string.Empty;
        }
        return Task.FromResult(values.Count == 0 ? null : new Secret(values));
    }

    static string Normalize(string value) => new([.. value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);

}

/// <summary>
/// Reads secrets from files: each secret is a sub-directory, each of whose files is a key, as Kubernetes mounts secrets. The secrets of a namespace are read from
/// <c>{directory}/namespaces/{namespace}/{name}</c>, and shared secrets from <c>{directory}/{name}</c>
/// </summary>
/// <param name="options">The service used to access the current <see cref="SecretOptions"/></param>
/// <remarks>Trailing line breaks are ignored, so that a file written with a final line break holds the value without it</remarks>
public class FileSecretProvider(IOptionsMonitor<SecretOptions> options)
    : ISecretProvider
{

    /// <inheritdoc/>
    public string Name => SecretProviders.File;

    /// <inheritdoc/>
    public virtual async Task<Secret?> GetAsync(string path, string? @namespace, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directoryRoot = Path.GetFullPath(options.CurrentValue.File.Directory);
        var namespacesRoot = Path.Combine(directoryRoot, SecretOptions.NamespacesSegment);
        var root = @namespace == null ? directoryRoot : Path.Combine(namespacesRoot, @namespace);
        var directory = Path.GetFullPath(Path.Combine(root, path));
        // a path cannot escape the directory of its namespace, e.g. with '..', nor designate the secrets of namespaces when it is the path of a shared secret
        if (!IsWithin(directory, root)) throw new InvalidOperationException($"The secret '{path}' is outside the directory secrets are read from");
        if (@namespace == null && (IsWithin(directory, namespacesRoot) || directory == namespacesRoot)) return null;
        if (!Directory.Exists(directory)) return null;
        var values = new Dictionary<string, string>();
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var key = Path.GetFileName(file);
            // Kubernetes mounts each version of a secret in a hidden directory, linked to by the visible files
            if (key.StartsWith('.')) continue;
            values[key] = (await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false)).TrimEnd('\r', '\n');
        }
        return values.Count == 0 ? null : new Secret(values);
    }

    static bool IsWithin(string path, string root) => path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

}

/// <summary>
/// Reads Kubernetes secrets through the Kubernetes API, authenticated with the service account of the pod
/// </summary>
/// <param name="httpClientFactory">The service used to create <see cref="HttpClient"/>s</param>
/// <param name="options">The service used to access the current <see cref="SecretOptions"/></param>
/// <remarks>
/// <para>
/// Only secrets with the configured label, <c>cloud-streams.io/secret=true</c> by default, are read, so that the broker and the gateway cannot read every secret of
/// their namespace even when their role allows them to. The service account needs to be allowed to get secrets.
/// </para>
/// <para>
/// The secrets of a Cloud Streams namespace are labelled <c>cloud-streams.io/namespace={namespace}</c>, which only that namespace may read. Secrets without that
/// label are shared (see #215)
/// </para>
/// </remarks>
public class KubernetesSecretProvider(IHttpClientFactory httpClientFactory, IOptionsMonitor<SecretOptions> options)
    : ISecretProvider
{

    /// <summary>
    /// Gets the name of the <see cref="HttpClient"/> the provider uses, which trusts the cluster's certificate authority
    /// </summary>
    public const string HttpClientName = "cloud-streams-kubernetes";

    /// <summary>
    /// Gets the label that binds a secret to a Cloud Streams namespace
    /// </summary>
    public const string NamespaceLabel = "cloud-streams.io/namespace";

    /// <inheritdoc/>
    public string Name => SecretProviders.Kubernetes;

    /// <inheritdoc/>
    public virtual async Task<Secret?> GetAsync(string path, string? @namespace, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var configuration = options.CurrentValue.Kubernetes;
        var server = configuration.Server ?? GetInClusterServer();
        var sharedNamespace = configuration.Namespace ?? await ReadServiceAccountFileAsync(configuration.ServiceAccountDirectory, "namespace", cancellationToken).ConfigureAwait(false);
        // in Kubernetes mode, the secrets of a namespace are those of its Kubernetes namespace, where no label is needed to tell whose they are (see #281). The
        // namespace of the shared secrets keeps telling them apart with the namespace label, should it also be the Kubernetes namespace of a namespace
        var kubernetesNamespace = configuration.NamespacedSecrets && !string.IsNullOrWhiteSpace(@namespace)
            ? (@namespace == SecretOptions.DefaultSharedNamespace ? configuration.DefaultNamespace : @namespace)
            : sharedNamespace;
        var ownedByNamespace = kubernetesNamespace != sharedNamespace;
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(server, $"api/v1/namespaces/{Uri.EscapeDataString(kubernetesNamespace)}/secrets/{Uri.EscapeDataString(path)}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await ReadServiceAccountFileAsync(configuration.ServiceAccountDirectory, "token", cancellationToken).ConfigureAwait(false));
        using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Kubernetes did not provide the secret '{path}' of the namespace '{kubernetesNamespace}': it responded with '{(int)response.StatusCode} {response.StatusCode}'. Is the service account allowed to get secrets?");
        var secret = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))!;
        // a secret of another namespace, or of a namespace when a shared one is read, is answered as missing rather than refused, so as not to reveal it
        if (!ownedByNamespace && secret["metadata"]?["labels"]?[NamespaceLabel]?.GetValue<string>() != @namespace) return null;
        if (!string.IsNullOrWhiteSpace(configuration.LabelSelector))
        {
            var separator = configuration.LabelSelector.IndexOf('=');
            var label = separator < 0 ? configuration.LabelSelector : configuration.LabelSelector[..separator];
            var expected = separator < 0 ? null : configuration.LabelSelector[(separator + 1)..];
            var actual = secret["metadata"]?["labels"]?[label]?.GetValue<string>();
            if (actual == null || (expected != null && actual != expected)) throw new InvalidOperationException($"The Kubernetes secret '{path}' cannot be read by Cloud Streams: label it with '{configuration.LabelSelector}'");
        }
        var values = new Dictionary<string, string>();
        foreach (var (key, value) in secret["data"]?.AsObject() ?? [])
        {
            if (value?.GetValue<string>() is { } encoded) values[key] = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        return new Secret(values);
    }

    static Uri GetInClusterServer()
    {
        var host = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
        var port = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_PORT");
        if (string.IsNullOrWhiteSpace(host)) throw new InvalidOperationException($"The Kubernetes secret provider only works in a Kubernetes pod, unless '{SecretOptions.ConfigurationSection}:Kubernetes:Server' is set");
        return new($"https://{(host.Contains(':') ? $"[{host}]" : host)}:{port ?? "443"}/");
    }

    static async Task<string> ReadServiceAccountFileAsync(string directory, string name, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, name);
        if (!File.Exists(path)) throw new InvalidOperationException($"The Kubernetes secret provider requires the pod's service account, whose '{name}' is not mounted at '{path}'");
        return (await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Trim();
    }

}

/// <summary>
/// Reads secrets from the KV secrets engine of HashiCorp Vault, version 1 or 2, authenticated with a token, the pod's Kubernetes service account, or an AppRole
/// </summary>
/// <param name="httpClientFactory">The service used to create <see cref="HttpClient"/>s</param>
/// <param name="options">The service used to access the current <see cref="SecretOptions"/></param>
/// <param name="timeProvider">The service used to get the current time</param>
/// <remarks>
/// What the broker and the gateway can read is what the policies of their Vault identity allow. The secrets of a namespace are read from
/// <c>{prefix}/namespaces/{namespace}/{path}</c>, and shared secrets from <c>{prefix}/{path}</c> (see #215)
/// </remarks>
public class VaultSecretProvider(IHttpClientFactory httpClientFactory, IOptionsMonitor<SecretOptions> options, TimeProvider timeProvider)
    : ISecretProvider
{

    /// <summary>
    /// Gets the name of the <see cref="HttpClient"/> the provider uses
    /// </summary>
    public const string HttpClientName = "cloud-streams-vault";

    readonly SemaphoreSlim _loginLock = new(1, 1);
    (string Token, DateTimeOffset ExpiresAt)? _token;

    /// <inheritdoc/>
    public string Name => SecretProviders.Vault;

    /// <inheritdoc/>
    public virtual async Task<Secret?> GetAsync(string path, string? @namespace, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var configuration = options.CurrentValue.Vault;
        if (configuration.Address == null) throw new InvalidOperationException($"The Vault secret provider requires '{SecretOptions.ConfigurationSection}:Vault:Address'");
        var mount = configuration.Mount.Trim('/');
        var relativePath = path.Trim('/');
        // a shared secret cannot be read from the path that holds the secrets of namespaces
        if (@namespace == null && (relativePath == SecretOptions.NamespacesSegment || relativePath.StartsWith(SecretOptions.NamespacesSegment + "/", StringComparison.Ordinal))) return null;
        if (@namespace != null) relativePath = $"{SecretOptions.NamespacesSegment}/{@namespace}/{relativePath}";
        var fullPath = $"{configuration.Prefix?.Trim('/')}{(string.IsNullOrWhiteSpace(configuration.Prefix) ? string.Empty : "/")}{relativePath}";
        var relative = configuration.KvVersion == 1 ? $"v1/{mount}/{fullPath}" : $"v1/{mount}/data/{fullPath}";
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(configuration.Address, relative));
        request.Headers.TryAddWithoutValidation("X-Vault-Token", await this.GetTokenAsync(configuration, cancellationToken).ConfigureAwait(false));
        if (!string.IsNullOrWhiteSpace(configuration.Namespace)) request.Headers.TryAddWithoutValidation("X-Vault-Namespace", configuration.Namespace);
        using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (response.StatusCode == HttpStatusCode.Forbidden) this._token = null;
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Vault did not provide the secret '{fullPath}': it responded with '{(int)response.StatusCode} {response.StatusCode}'. Does the policy of Cloud Streams allow reading it?");
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))!;
        var data = (configuration.KvVersion == 1 ? body["data"] : body["data"]?["data"])?.AsObject();
        if (data == null) return null;
        return new Secret(data.ToDictionary(p => p.Key, p => p.Value is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : p.Value?.ToJsonString() ?? string.Empty));
    }

    /// <summary>
    /// Gets a Vault token, logging in if the configured method requires it, and the previous token has expired
    /// </summary>
    protected virtual async Task<string> GetTokenAsync(VaultSecretProviderOptions configuration, CancellationToken cancellationToken)
    {
        var method = configuration.Authentication.Method.Trim().ToLowerInvariant();
        if (method == "token") return configuration.Authentication.Token ?? Environment.GetEnvironmentVariable("VAULT_TOKEN") ?? throw new InvalidOperationException($"The Vault secret provider requires '{SecretOptions.ConfigurationSection}:Vault:Authentication:Token', or the VAULT_TOKEN environment variable");
        if (this._token is { } token && token.ExpiresAt > timeProvider.GetUtcNow()) return token.Token;
        await this._loginLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this._token is { } current && current.ExpiresAt > timeProvider.GetUtcNow()) return current.Token;
            JsonObject payload = method switch
            {
                "kubernetes" => new()
                {
                    ["role"] = configuration.Authentication.Role ?? throw new InvalidOperationException($"The Vault kubernetes authentication requires '{SecretOptions.ConfigurationSection}:Vault:Authentication:Role'"),
                    ["jwt"] = (await File.ReadAllTextAsync(Path.Combine(options.CurrentValue.Kubernetes.ServiceAccountDirectory, "token"), cancellationToken).ConfigureAwait(false)).Trim()
                },
                "approle" => new()
                {
                    ["role_id"] = configuration.Authentication.RoleId ?? throw new InvalidOperationException($"The Vault approle authentication requires '{SecretOptions.ConfigurationSection}:Vault:Authentication:RoleId'"),
                    ["secret_id"] = configuration.Authentication.SecretId
                },
                _ => throw new InvalidOperationException($"'{method}' is not a supported Vault authentication method: use token, kubernetes or approle")
            };
            var mountPath = (configuration.Authentication.MountPath ?? method).Trim('/');
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(configuration.Address!, $"v1/auth/{mountPath}/login")) { Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json") };
            if (!string.IsNullOrWhiteSpace(configuration.Namespace)) request.Headers.TryAddWithoutValidation("X-Vault-Namespace", configuration.Namespace);
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Failed to log in to Vault with the '{method}' method: it responded with '{(int)response.StatusCode} {response.StatusCode}'");
            var auth = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))?["auth"] ?? throw new InvalidOperationException("Vault's login response has no 'auth'");
            var clientToken = auth["client_token"]!.GetValue<string>();
            var lease = auth["lease_duration"]?.GetValue<long>() ?? 300;
            // the token is renewed by logging in again before it expires
            this._token = (clientToken, timeProvider.GetUtcNow().AddSeconds(Math.Max(lease * 0.8, 1)));
            return clientToken;
        }
        finally
        {
            this._loginLock.Release();
        }
    }

}
