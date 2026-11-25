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

using Neuroglia;
using System.Globalization;
using Neuroglia.Eventing.CloudEvents;

namespace CloudStreams.Core.Api.Client.Services;

public partial class CloudStreamsCoreApiClient
    : ICloudEventsApi, ICloudEventPartitionsApi, ICloudEventStreamApi
{

    /// <summary>
    /// Gets the relative path to the Cloud Streams cloud events API
    /// </summary>
    public const string CloudEventsApiPath = ApiVersionPathPrefix + "core/v1/cloud-events/";
    private const string CloudEventPartitionsApiPath = CloudEventsApiPath + "partitions/";
    private const string CloudEventStreamApiPath = CloudEventsApiPath + "stream/";

    ICloudEventPartitionsApi ICloudEventsApi.Partitions => this;

    ICloudEventStreamApi ICloudEventsApi.Stream => this;

    /// <inheritdoc/>
    public virtual async Task<PartitionIdCollection> ListPartitionsByTypeAsync(CloudEventPartitionType type, string? prefix = null, int? maxResults = null, string? continuationToken = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(prefix)) query.Add($"prefix={Uri.EscapeDataString(prefix)}");
        if (maxResults.HasValue) query.Add($"maxResults={maxResults.Value}");
        if (!string.IsNullOrWhiteSpace(continuationToken)) query.Add($"continuationToken={Uri.EscapeDataString(continuationToken)}");
        var uri = $"{CloudEventPartitionsApiPath}{type}{(query.Count > 0 ? "?" + string.Join('&', query) : string.Empty)}";
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<PartitionIdCollection>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<PartitionMetadataCollection> ListPartitionMetadataByTypeAsync(CloudEventPartitionType type, string? prefix = null, int? maxResults = null, string? continuationToken = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(prefix)) query.Add($"prefix={Uri.EscapeDataString(prefix)}");
        if (maxResults.HasValue) query.Add($"maxResults={maxResults.Value}");
        if (!string.IsNullOrWhiteSpace(continuationToken)) query.Add($"continuationToken={Uri.EscapeDataString(continuationToken)}");
        var uri = $"{CloudEventPartitionsApiPath}{type}/metadata{(query.Count > 0 ? "?" + string.Join('&', query) : string.Empty)}";
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<PartitionMetadataCollection>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<StreamActivity> GetStreamActivityAsync(TimeSpan? period = null, int? buckets = null, int? sampleSize = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (period.HasValue) query.Add($"period={Uri.EscapeDataString(System.Xml.XmlConvert.ToString(period.Value))}");
        if (buckets.HasValue) query.Add($"buckets={buckets.Value}");
        if (sampleSize.HasValue) query.Add($"sampleSize={sampleSize.Value}");
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, $"{CloudEventStreamApiPath}activity{(query.Count > 0 ? "?" + string.Join('&', query) : string.Empty)}"), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<StreamActivity>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<PartitionMetadata?> GetPartitionMetadataAsync(CloudEventPartitionType type, string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, $"{CloudEventPartitionsApiPath}{type}/byId?id={Uri.EscapeDataString(id)}"), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<PartitionMetadata>(json);
    }

    /// <inheritdoc/>
    public virtual async Task<CloudEventSearchResult> SearchAsync(CloudEventSearchOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var query = new List<string>();
        void Add(string name, object? value) { if (value != null && value.ToString() is { Length: > 0 } text) query.Add($"{name}={Uri.EscapeDataString(text)}"); }
        Add("partitionType", options.PartitionType.HasValue ? EnumHelper.Stringify(options.PartitionType.Value) : null);
        Add("partitionId", options.PartitionId);
        Add("offset", options.Offset);
        Add("from", options.From?.ToString("O", CultureInfo.InvariantCulture));
        Add("to", options.To?.ToString("O", CultureInfo.InvariantCulture));
        Add("id", options.Id);
        Add("expression", options.Expression);
        Add("maxResults", options.MaxResults);
        Add("maxScanned", options.MaxScanned);
        foreach (var (name, pattern) in options.Attributes ?? []) Add($"attributes[{Uri.EscapeDataString(name)}]", pattern);
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, $"{CloudEventsApiPath}search{(query.Count > 0 ? "?" + string.Join('&', query) : string.Empty)}"), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<CloudEventSearchResult>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<CloudEventPublication> PublishAsync(CloudEvent e, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        using var content = new StringContent(this.Serializer.SerializeToText(e), System.Text.Encoding.UTF8, "application/json");
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Post, $"{CloudEventsApiPath}publish") { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<CloudEventPublication>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<StreamMetadata> GetStreamMetadataAsync(CancellationToken cancellationToken = default)
    {
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, CloudEventStreamApiPath), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<StreamMetadata>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<IAsyncEnumerable<CloudEvent?>> ReadStreamAsync(StreamReadOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (options == null) options = new();
        var uri = $"{CloudEventStreamApiPath}read";
        var queryParameters = new Dictionary<string, object>() { { $"{nameof(options.Direction)}".ToCamelCase(), options.Direction.ToString() } };
        if (options.Partition != null)
        {
            queryParameters.Add($"{nameof(options.Partition).ToCamelCase()}.{nameof(options.Partition.Type).ToCamelCase()}", options.Partition.Type.ToString());
            queryParameters.Add($"{nameof(options.Partition).ToCamelCase()}.{nameof(options.Partition.Id).ToCamelCase()}", options.Partition.Id);
        }
        if (options.Offset.HasValue) queryParameters.Add(nameof(options.Offset).ToCamelCase(), options.Offset.Value);
        queryParameters.Add(nameof(options.Length).ToCamelCase(), options.Length);
        // values are escaped: partition ids are often URIs, which carry their own query strings (see #178)
        uri += $"?{string.Join('&', queryParameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(Convert.ToString(p.Value, CultureInfo.InvariantCulture) ?? string.Empty)}"))}";
        var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken).ConfigureAwait(false);
        var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return this.Serializer.DeserializeAsyncEnumerable<CloudEvent>(responseStream, cancellationToken: cancellationToken);
    }

}
