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
    public virtual Task<Subscription> SuspendAsync(string name, CancellationToken cancellationToken = default) => this.PostAsync(name, "suspend", cancellationToken);

    /// <inheritdoc/>
    public virtual Task<Subscription> ResumeAsync(string name, CancellationToken cancellationToken = default) => this.PostAsync(name, "resume", cancellationToken);

    /// <inheritdoc/>
    public virtual Task<Subscription> ReplayAsync(string name, DateTimeOffset? from = null, CancellationToken cancellationToken = default) => this.PostAsync(name, from.HasValue ? $"replay?from={Uri.EscapeDataString(from.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture))}" : "replay", cancellationToken);

    /// <inheritdoc/>
    public virtual Task<Subscription> SkipAsync(string name, ulong offset, CancellationToken cancellationToken = default) => this.PostAsync(name, $"skip?offset={offset}", cancellationToken);

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyList<SubscriptionHealth>> GetHealthAsync(IEnumerable<LabelSelector>? labelSelectors = null, CancellationToken cancellationToken = default)
    {
        var selectors = labelSelectors?.ToList();
        var uri = selectors?.Count > 0 ? $"{this.Path}/health?labelSelector={Uri.EscapeDataString(string.Join(',', selectors))}" : $"{this.Path}/health";
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<List<SubscriptionHealth>>(json) ?? [];
    }

    /// <inheritdoc/>
    public virtual async Task<SubscriptionTestResult> TestAsync(SubscriptionTestRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var content = new StringContent(this.Serializer.SerializeToText(request), System.Text.Encoding.UTF8, "application/json");
        using var message = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Post, $"{this.Path}/test") { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(message, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<SubscriptionTestResult>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyList<DeadLetter>> GetDeadLettersAsync(string name, long? offset = null, ulong length = StreamReadOptions.MaxLength, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        var query = $"length={length}" + (offset.HasValue ? $"&offset={offset.Value}" : string.Empty);
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, $"{this.GetResourceUri(name)}/dead-letters?{query}"), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<List<DeadLetter>>(json) ?? [];
    }

    /// <inheritdoc/>
    public virtual async Task<CloudEventRedelivery> RedeliverAsync(string name, CloudEventRedeliveryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        ArgumentNullException.ThrowIfNull(request);
        using var content = new StringContent(this.Serializer.SerializeToText(request), System.Text.Encoding.UTF8, "application/json");
        using var message = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Post, $"{this.GetResourceUri(name)}/redeliver") { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(message, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<CloudEventRedelivery>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<DeadLetterRedelivery> RedeliverDeadLetterAsync(string name, ulong offset, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Post, $"{this.GetResourceUri(name)}/dead-letters/{offset}/redeliver"), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<DeadLetterRedelivery>(json)!;
    }

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
