using CloudStreams.Core.Data.Models;
using System.Text;

namespace CloudStreams.Gateway.Api.Client.Services;

public partial class CloudStreamsGatewayApiClient
    : ICloudEventsApi
{

    private const string ApiVersionPathPrefix = "api/gateway/v1/";
    private const string CloudEventsApiPath = ApiVersionPathPrefix + "cloud-events/";

    /// <inheritdoc/>
    public virtual async Task PublishCloudEventAsync(CloudEvent e, CancellationToken cancellationToken = default)
    {
        if (e == null) throw new ArgumentNullException(nameof(e));
        var json = Serializer.Json.Serialize(e);
        using var content = new StringContent(json, Encoding.UTF8, CloudEventMediaTypeNames.CloudEventsJson);
        using var request = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Post, $"{CloudEventsApiPath}pub") { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

}