namespace CloudStreams.Core.Api.Client.Services;

public partial class CloudStreamsApiClient
    : ICloudEventsApi, ICloudEventPartitionsApi, ICloudEventStreamApi
{

    private const string ApiVersionPathPrefix = "api/core/v1/";
    private const string CloudEventsApiPath = ApiVersionPathPrefix + "cloud-events/";
    private const string CloudEventPartitionsApiPath = CloudEventsApiPath + "partitions/";
    private const string CloudEventStreamApiPath = CloudEventsApiPath + "stream/";

    ICloudEventPartitionsApi ICloudEventsApi.Partitions => this;

    ICloudEventStreamApi ICloudEventsApi.Stream => this;

    /// <inheritdoc/>
    public virtual async Task<IAsyncEnumerable<string?>> ListPartitionsByTypeAsync(CloudEventPartitionType type, CancellationToken cancellationToken = default)
    {
        var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, $"{CloudEventPartitionsApiPath}{type}"), cancellationToken).ConfigureAwait(false);
        var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return Serializer.Json.DeserializeAsyncEnumerable<string>(responseStream, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async Task<PartitionMetadata?> GetPartitionMetadataAsync(CloudEventPartitionType type, string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, $"{CloudEventPartitionsApiPath}{type}/{id}"), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Serializer.Json.Deserialize<PartitionMetadata>(json);
    }

    /// <inheritdoc/>
    public virtual async Task<StreamMetadata> GetStreamMetadataAsync(CancellationToken cancellationToken = default)
    {
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, CloudEventStreamApiPath), cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Serializer.Json.Deserialize<StreamMetadata>(json)!;
    }

    /// <inheritdoc/>
    public virtual async Task<IAsyncEnumerable<CloudEvent?>> ReadStreamAsync(StreamReadOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (options == null) options = new();
        var uri = $"{CloudEventStreamApiPath}read";
        var queryParameters = new Dictionary<string, object>() { { $"{nameof(options.Direction)}".ToCamelCase(), options.Direction.ToString() } };
        if(options.Partition != null)
        {
            queryParameters.Add($"{nameof(options.Partition)}.{nameof(options.Partition.Type)}".ToCamelCase(), options.Partition.Type.ToString());
            queryParameters.Add($"{nameof(options.Partition)}.{nameof(options.Partition.Id)}".ToCamelCase(), options.Partition.Id);
        }
        if (options.Offset.HasValue) queryParameters.Add(nameof(options.Offset).ToCamelCase(), options.Offset.Value);
        queryParameters.Add(nameof(options.Length).ToCamelCase(), options.Length);
        uri += $"?{string.Join('&', queryParameters.Select(p => $"{p.Key}={p.Value}"))}";
        var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken).ConfigureAwait(false);
        var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return Serializer.Json.DeserializeAsyncEnumerable<CloudEvent>(responseStream, cancellationToken: cancellationToken);
    }

}