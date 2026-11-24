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

using Microsoft.Extensions.Logging;
using Neuroglia.Serialization;

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Represents the default implementation of the <see cref="ISubscriptionManagementApi"/>
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="serializer">The service used to serialize/deserialize objects from/to JSON</param>
/// <param name="httpClient">The service used to perform HTTP requests</param>
/// <param name="path">The path to the API used to manage <see cref="Subscription"/>s</param>
public class SubscriptionManagementApi(ILogger<ResourceManagementApi<Subscription>> logger, IJsonSerializer serializer, HttpClient httpClient, string path)
    : ResourceManagementApi<Subscription>(logger, serializer, httpClient, path), ISubscriptionManagementApi
{

    /// <inheritdoc/>
    public virtual Task<Subscription> ResumeAsync(string name, CancellationToken cancellationToken = default) => this.PostAsync(name, "resume", cancellationToken);

    /// <inheritdoc/>
    public virtual Task<Subscription> ReplayAsync(string name, CancellationToken cancellationToken = default) => this.PostAsync(name, "replay", cancellationToken);

    /// <summary>
    /// Posts an operation to the specified <see cref="Subscription"/>
    /// </summary>
    /// <param name="name">The name of the <see cref="Subscription"/> to operate</param>
    /// <param name="operation">The name of the operation to perform</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The operated <see cref="Subscription"/></returns>
    protected virtual async Task<Subscription> PostAsync(string name, string operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Post, $"{this.GetResourceUri(name)}/{operation}"), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<Subscription>(json)!;
    }

}
