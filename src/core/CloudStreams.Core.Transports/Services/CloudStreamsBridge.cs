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

using Microsoft.Extensions.Options;
using Neuroglia.Eventing.CloudEvents;
using Neuroglia.Serialization;
using System.Net;
using System.Net.Http.Headers;

namespace CloudStreams.Core.Transports.Services;

/// <summary>
/// Delivers cloud events to the ingestion endpoint of another Cloud Streams gateway, authenticated with the API key or the token of the configured credentials (see #218)
/// </summary>
/// <param name="httpClientFactory">The service used to create <see cref="HttpClient"/>s</param>
/// <param name="options">The service used to access the current <see cref="TransportOptions"/></param>
/// <param name="serializer">The service used to serialize cloud events</param>
/// <remarks>
/// <para>Batches are published as <c>application/cloudevents-batch+json</c>, in a single request. The other gateway stores the cloud events of a batch independently:
/// if it refuses one, the whole batch is delivered again, so that enabling deduplication on the other gateway keeps the cloud events it already stored from being
/// stored twice.</para>
/// <para>The other gateway's split-horizon policy, if any, recognizes the cloud events that come back to it, so that two instances can forward cloud events to each
/// other without looping.</para>
/// </remarks>
public sealed class CloudStreamsBridge(IHttpClientFactory httpClientFactory, IOptionsMonitor<TransportOptions> options, IJsonSerializer serializer)
    : ICloudEventSink
{

    /// <summary>
    /// Gets the name of the <see cref="HttpClient"/> the bridge uses
    /// </summary>
    public const string HttpClientName = "cloud-streams-bridge";

    const string DefaultApiKeyHeader = "X-Api-Key";

    /// <inheritdoc/>
    public IReadOnlyList<string> Schemes { get; } = [TransportSchemes.CloudStreams];

    /// <summary>
    /// Gets the address of the ingestion endpoint of the gateway the specified address designates
    /// </summary>
    /// <param name="address">The address of the gateway</param>
    /// <returns>The address of the gateway's ingestion endpoint</returns>
    public static Uri GetEndpoint(TransportAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var path = address.Path.Trim('/');
        return new($"{(address.Tls ? "https" : "http")}://{address.Server}/{(path.Length == 0 ? string.Empty : path + "/")}api/events/pub");
    }

    /// <inheritdoc/>
    public async Task SendAsync(TransportAddress address, IReadOnlyList<CloudEvent> events, IReadOnlyList<KeyValuePair<string, string>> headers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(headers);
        if (events.Count == 0) return;
        var credentials = options.CurrentValue.GetCredentials(address);
        var endpoint = GetEndpoint(address);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = events.Count == 1 ? events[0].ToHttpContent(serializer) : events.ToBatchHttpContent(serializer)
        };
        foreach (var (name, value) in headers) request.Headers.TryAddWithoutValidation(name, value);
        if (credentials?.ApiKey != null) request.Headers.TryAddWithoutValidation(credentials.ApiKeyHeader ?? DefaultApiKeyHeader, credentials.ApiKey);
        else if (credentials?.Token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Token);
        using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var reason = $"The gateway '{endpoint}' responded with a non-success status code '{response.StatusCode}'";
            throw new HttpRequestException(string.IsNullOrWhiteSpace(content) ? reason : $"{reason}: {content}", null, response.StatusCode);
        }
        // a batch is answered with the result of each of its cloud events, any of which may have been refused
        if (events.Count == 1 || response.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(content)) return;
        var results = serializer.Deserialize<List<CloudEventIngestionResult>>(content) ?? [];
        var refused = results.FirstOrDefault(r => r.Status is < 200 or >= 300);
        if (refused == null) return;
        var detail = refused.Problem?.Detail ?? refused.Problem?.Title;
        throw new HttpRequestException($"The gateway '{endpoint}' refused the cloud event '{refused.Id}' of the batch, at index {refused.Index}, with status code '{refused.Status}'{(detail == null ? string.Empty : $": {detail}")}", null, (HttpStatusCode)refused.Status);
    }

}
