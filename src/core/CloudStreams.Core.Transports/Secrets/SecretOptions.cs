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

namespace CloudStreams.Core.Transports.Secrets;

/// <summary>
/// Represents the options used to configure the providers the broker and the gateway read secrets from (see #218)
/// </summary>
public class SecretOptions
{

    /// <summary>
    /// Gets the path of the configuration section that holds the secret options
    /// </summary>
    public const string ConfigurationSection = "CloudStreams:Secrets";

    /// <summary>
    /// Gets/sets the providers to read secrets from, in the order in which a secret referred to by name only is looked for. See <see cref="SecretProviders"/>.
    /// Defaults to the environment and files
    /// </summary>
    public virtual List<string> Providers { get; set; } = [];

    /// <summary>
    /// Gets/sets how long a secret is cached, after which it is read again, so that rotated secrets are picked up. Defaults to a minute
    /// </summary>
    public virtual TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets/sets the options of the file provider
    /// </summary>
    public virtual FileSecretProviderOptions File { get; set; } = new();

    /// <summary>
    /// Gets/sets the options of the Kubernetes provider
    /// </summary>
    public virtual KubernetesSecretProviderOptions Kubernetes { get; set; } = new();

    /// <summary>
    /// Gets/sets the options of the Vault provider
    /// </summary>
    public virtual VaultSecretProviderOptions Vault { get; set; } = new();

    /// <summary>
    /// Gets the providers to read secrets from, in order
    /// </summary>
    /// <returns>The providers to read secrets from</returns>
    public virtual IReadOnlyList<string> GetProviders() => this.Providers.Count > 0 ? [.. this.Providers.Select(p => p.Trim().ToLowerInvariant()).Distinct()] : [SecretProviders.Environment, SecretProviders.File];

}

/// <summary>
/// Represents the options of the provider that reads secrets from files, such as mounted Kubernetes secrets
/// </summary>
public class FileSecretProviderOptions
{

    /// <summary>
    /// Gets the directory secrets are read from by default
    /// </summary>
    public const string DefaultDirectory = "/var/run/secrets/cloud-streams";

    /// <summary>
    /// Gets/sets the directory secrets are read from: each secret is a sub-directory, each of whose files is a key. Defaults to <c>/var/run/secrets/cloud-streams</c>
    /// </summary>
    public virtual string Directory { get; set; } = DefaultDirectory;

}

/// <summary>
/// Represents the options of the provider that reads Kubernetes secrets through the Kubernetes API
/// </summary>
public class KubernetesSecretProviderOptions
{

    /// <summary>
    /// Gets the label secrets must have, to be read by default
    /// </summary>
    public const string DefaultLabelSelector = "cloud-streams.io/secret=true";

    /// <summary>
    /// Gets/sets the namespace secrets are read from. Defaults to that of the pod
    /// </summary>
    public virtual string? Namespace { get; set; }

    /// <summary>
    /// Gets/sets the label, as <c>key=value</c>, secrets must have to be read, so that the broker and the gateway cannot read every secret of the namespace. Defaults
    /// to <c>cloud-streams.io/secret=true</c>. Empty to read any secret the service account is allowed to
    /// </summary>
    public virtual string? LabelSelector { get; set; } = DefaultLabelSelector;

    /// <summary>
    /// Gets/sets the address of the Kubernetes API. Defaults to the in-cluster address
    /// </summary>
    public virtual Uri? Server { get; set; }

    /// <summary>
    /// Gets/sets the directory the credentials of the pod's service account are mounted in. Defaults to <c>/var/run/secrets/kubernetes.io/serviceaccount</c>
    /// </summary>
    public virtual string ServiceAccountDirectory { get; set; } = "/var/run/secrets/kubernetes.io/serviceaccount";

}

/// <summary>
/// Represents the options of the provider that reads secrets from the KV secrets engine of HashiCorp Vault
/// </summary>
public class VaultSecretProviderOptions
{

    /// <summary>
    /// Gets/sets the address of Vault, e.g. <c>https://vault:8200</c>
    /// </summary>
    public virtual Uri? Address { get; set; }

    /// <summary>
    /// Gets/sets the path the KV secrets engine is mounted at. Defaults to <c>secret</c>
    /// </summary>
    public virtual string Mount { get; set; } = "secret";

    /// <summary>
    /// Gets/sets the version of the KV secrets engine, 1 or 2. Defaults to 2
    /// </summary>
    public virtual int KvVersion { get; set; } = 2;

    /// <summary>
    /// Gets/sets a prefix to the paths of secrets, e.g. <c>cloud-streams/</c>, if any
    /// </summary>
    public virtual string? Prefix { get; set; }

    /// <summary>
    /// Gets/sets the Vault namespace, with Vault Enterprise, if any
    /// </summary>
    public virtual string? Namespace { get; set; }

    /// <summary>
    /// Gets/sets how to authenticate to Vault
    /// </summary>
    public virtual VaultAuthenticationOptions Authentication { get; set; } = new();

}

/// <summary>
/// Represents the options used to authenticate to Vault
/// </summary>
public class VaultAuthenticationOptions
{

    /// <summary>
    /// Gets/sets the authentication method: <c>token</c> (default), <c>kubernetes</c> or <c>approle</c>
    /// </summary>
    public virtual string Method { get; set; } = "token";

    /// <summary>
    /// Gets/sets the token, with the <c>token</c> method. Defaults to the <c>VAULT_TOKEN</c> environment variable
    /// </summary>
    public virtual string? Token { get; set; }

    /// <summary>
    /// Gets/sets the role, with the <c>kubernetes</c> method
    /// </summary>
    public virtual string? Role { get; set; }

    /// <summary>
    /// Gets/sets the id of the role, with the <c>approle</c> method
    /// </summary>
    public virtual string? RoleId { get; set; }

    /// <summary>
    /// Gets/sets the secret id, with the <c>approle</c> method
    /// </summary>
    public virtual string? SecretId { get; set; }

    /// <summary>
    /// Gets/sets the path the authentication method is mounted at. Defaults to the name of the method
    /// </summary>
    public virtual string? MountPath { get; set; }

}
