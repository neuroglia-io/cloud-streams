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

using Json.Schema;
using Neuroglia.Serialization;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Represents the in-memory implementation of the <see cref="IJsonSchemaRegistry"/> interface
/// </summary>
/// <param name="httpClient">The service used to perform HTTP requests</param>
/// <param name="serializer">The service used to serialize/deserialize objects to/from JSON</param>
public class MemoryJsonSchemaRegistry(HttpClient httpClient, IJsonSerializer serializer)
    : IJsonSchemaRegistry
{

    /// <summary>
    /// Gets the service used to perform HTTP requests
    /// </summary>
    protected HttpClient HttpClient { get; } = httpClient;

    /// <summary>
    /// Gets the service used to serialize/deserialize objects to/from JSON
    /// </summary>
    protected IJsonSerializer Serializer { get; } = serializer;

    /// <inheritdoc/>
    public virtual async Task<JsonSchema> GetAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var document = (JsonNodeBaseDocument?)SchemaRegistry.Global.Get(uri);
        if (document != null) return this.Serializer.Deserialize<JsonSchema>(this.Serializer.SerializeToText(document))!;
        var json = await this.HttpClient.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
        var schema = this.Serializer.Deserialize<JsonSchema>(json)!;
        document = new JsonNodeBaseDocument(this.Serializer.SerializeToNode(schema)!, uri);
        SchemaRegistry.Global.Register(uri, document);
        return schema;
    }

    /// <inheritdoc/>
    public virtual async Task RegisterAsync(JsonSchema schema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var document = new JsonNodeBaseDocument(this.Serializer.SerializeToNode(schema)!, schema.BaseUri);
        SchemaRegistry.Global.Register(schema.BaseUri, document);
        await Task.CompletedTask.ConfigureAwait(false);
    }

}
