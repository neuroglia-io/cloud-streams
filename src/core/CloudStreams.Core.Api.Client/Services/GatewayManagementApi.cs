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
using System.Text;

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Represents the default implementation of the <see cref="IGatewayManagementApi"/>
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="serializer">The service used to serialize/deserialize objects from/to JSON</param>
/// <param name="httpClient">The service used to perform HTTP requests</param>
/// <param name="path">The path to the API used to manage <see cref="Gateway"/>s</param>
public class GatewayManagementApi(ILogger<ResourceManagementApi<Gateway>> logger, IJsonSerializer serializer, HttpClient httpClient, string path)
    : ResourceManagementApi<Gateway>(logger, serializer, httpClient, path), IGatewayManagementApi
{

    /// <inheritdoc/>
    public virtual async Task<GatewayAdmissionTestResult> TestAdmissionAsync(string name, GatewayAdmissionTestRequest request, string? @namespace = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        ArgumentNullException.ThrowIfNull(request);
        using var content = new StringContent(this.Serializer.SerializeToText(request), Encoding.UTF8, "application/json");
        using var message = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Post, $"{this.GetResourceUri(name, @namespace)}/test") { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(message, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<GatewayAdmissionTestResult>(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))!;
    }

}

/// <summary>
/// Represents the default implementation of the <see cref="ISchemaManagementApi"/>
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="serializer">The service used to serialize/deserialize objects from/to JSON</param>
/// <param name="httpClient">The service used to perform HTTP requests</param>
/// <param name="path">The path to the API used to manage <see cref="Schema"/>s</param>
public class SchemaManagementApi(ILogger<ResourceManagementApi<Schema>> logger, IJsonSerializer serializer, HttpClient httpClient, string path)
    : ResourceManagementApi<Schema>(logger, serializer, httpClient, path), ISchemaManagementApi
{

    /// <inheritdoc/>
    public virtual async Task<SchemaCompatibilityResult> CheckCompatibilityAsync(string name, System.Text.Json.Nodes.JsonNode schema, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        ArgumentNullException.ThrowIfNull(schema);
        using var content = new StringContent(schema.ToJsonString(), Encoding.UTF8, "application/json");
        using var message = await this.ProcessRequestAsync(new HttpRequestMessage(HttpMethod.Post, $"{this.GetResourceUri(name, null)}/compatibility") { Content = content }, cancellationToken).ConfigureAwait(false);
        using var response = await this.ProcessResponseAsync(await this.HttpClient.SendAsync(message, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return this.Serializer.Deserialize<SchemaCompatibilityResult>(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))!;
    }

}
