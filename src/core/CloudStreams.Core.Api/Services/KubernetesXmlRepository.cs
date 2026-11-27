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
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.AspNetCore.DataProtection.Repositories;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the <see cref="IXmlRepository"/> that keeps the ASP.NET data-protection keys in a Kubernetes secret of the process' own namespace, in Kubernetes mode, so
/// that the replicas that serve a dashboard share them (see #280)
/// </summary>
/// <param name="kubernetes">The service used to interact with the Kubernetes API</param>
/// <param name="options">The options of the Kubernetes store of resources</param>
public partial class KubernetesXmlRepository(IKubernetes kubernetes, KubernetesResourceStoreOptions options)
    : IXmlRepository
{

    /// <summary>
    /// Gets the name of the secret that holds the keys
    /// </summary>
    public const string SecretName = "cloud-streams-data-protection";

    /// <inheritdoc/>
    public virtual IReadOnlyCollection<XElement> GetAllElements()
    {
        var secret = this.ReadAsync(CancellationToken.None).GetAwaiter().GetResult();
        return secret?.Data?.OrderBy(d => d.Key, StringComparer.Ordinal).Select(d => XElement.Parse(Encoding.UTF8.GetString(d.Value))).ToList() ?? [];
    }

    /// <inheritdoc/>
    public virtual void StoreElement(XElement element, string friendlyName) => this.StoreElementAsync(element, friendlyName, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Stores the specified key
    /// </summary>
    /// <param name="element">The key to store</param>
    /// <param name="friendlyName">The name of the key</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    protected virtual async Task StoreElementAsync(XElement element, string friendlyName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var key = $"{InvalidCharacters().Replace(string.IsNullOrWhiteSpace(friendlyName) ? Guid.NewGuid().ToString("N") : friendlyName, "-")}.xml";
        var value = Encoding.UTF8.GetBytes(element.ToString(SaveOptions.DisableFormatting));
        var @namespace = options.GetCurrentNamespace();
        for (var attempt = 1; ; attempt++)
        {
            var secret = await this.ReadAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (secret == null)
                {
                    secret = new V1Secret()
                    {
                        Metadata = new() { Name = SecretName, NamespaceProperty = @namespace, Labels = new Dictionary<string, string>() { { "app.kubernetes.io/managed-by", "cloud-streams" } } },
                        Type = "Opaque",
                        Data = new Dictionary<string, byte[]>() { [key] = value }
                    };
                    await kubernetes.CoreV1.CreateNamespacedSecretAsync(secret, @namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
                    return;
                }
                secret.Data ??= new Dictionary<string, byte[]>();
                secret.Data[key] = value;
                // the resource version the secret was read with guards against a replica that stores a key concurrently
                await kubernetes.CoreV1.ReplaceNamespacedSecretAsync(secret, SecretName, @namespace, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict && attempt < 5) { }
        }
    }

    /// <summary>
    /// Reads the secret that holds the keys, if it exists
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The secret that holds the keys, if any</returns>
    protected virtual async Task<V1Secret?> ReadAsync(CancellationToken cancellationToken)
    {
        try { return await kubernetes.CoreV1.ReadNamespacedSecretAsync(SecretName, options.GetCurrentNamespace(), cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound) { return null; }
    }

    [GeneratedRegex("[^-._a-zA-Z0-9]+")]
    private static partial Regex InvalidCharacters();

}
