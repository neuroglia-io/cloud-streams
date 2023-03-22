// Copyright © 2023-Present The Cloud Streams Authors
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