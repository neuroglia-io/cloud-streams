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

using Microsoft.Extensions.Logging;

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Services;

public partial class ApicurioRegistryApiClient
    : IVersionsApi
{

    async Task<string> IVersionsApi.GetArtifactVersionAsync(string artifactId, string groupId, string version, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(artifactId)) throw new ArgumentNullException(nameof(artifactId));
        if (string.IsNullOrWhiteSpace(groupId)) throw new ArgumentNullException(nameof(groupId));
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentNullException(nameof(version));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{PathPrefix}/groups/{groupId}/artifacts/{artifactId}/versions/{version}");
        using var response = await this.HttpClient.SendAsync(request, cancellationToken);
        var content = await response.Content?.ReadAsStringAsync(cancellationToken)!;
        if (!response.IsSuccessStatusCode)
        {
            this.Logger.LogError("An error occured while retrieving the artifact with the specified id '{artifactId}' and '{version}': the remote server responded with a non-success status code '{statusCode}'./r/Response content: {json}", artifactId, version, response.StatusCode, content);
            response.EnsureSuccessStatusCode();
        }
        return content;
    }

}