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

using CloudStreams.Core.Resources;
using Json.Schema;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Serialization;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Resolves the registered schemas the gateway's ingestion configurations bind cloud events to (see #213)
/// </summary>
/// <param name="serviceProvider">The service used to resolve the resource repository</param>
/// <param name="serializer">The service used to read the JSON schemas of versions</param>
/// <remarks>
/// Schemas are read again at most every <see cref="CacheDuration"/>, so that new versions apply shortly after they are registered without reading the schema for each
/// cloud event. Versions never change: their parsed JSON schemas are kept for as long as the schema's resource version is current
/// </remarks>
public class RegisteredSchemaResolver(IServiceProvider serviceProvider, IJsonSerializer serializer)
{

    /// <summary>
    /// Gets how long a schema is cached
    /// </summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    readonly ConcurrentDictionary<string, (DateTimeOffset ReadAt, Schema? Schema)> _schemas = new();
    readonly ConcurrentDictionary<(string Name, string? ResourceVersion, int Version), JsonSchema> _parsed = new();

    /// <summary>
    /// Resolves the specified schema version
    /// </summary>
    /// <param name="reference">The reference to the schema version to resolve</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The JSON schema and the number of the referenced version, or the reason why it could not be resolved</returns>
    public virtual async Task<(JsonSchema? Schema, int Version, string? Error)> ResolveAsync(SchemaReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var now = DateTimeOffset.UtcNow;
        if (!this._schemas.TryGetValue(reference.Name, out var cached) || now - cached.ReadAt > CacheDuration)
        {
            Schema? schema;
            try { schema = await serviceProvider.GetRequiredService<IResourceRepository>().GetAsync<Schema>(reference.Name, null, cancellationToken).ConfigureAwait(false); }
            catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound) { schema = null; }
            cached = (now, schema);
            this._schemas[reference.Name] = cached;
        }
        if (cached.Schema == null) return (null, 0, $"The schema '{reference.Name}' the cloud event is bound to does not exist");
        var version = cached.Schema.Spec.GetVersion(reference.Version);
        if (version == null) return (null, 0, $"The schema '{reference.Name}' has no version '{reference.Version}'");
        var parsed = this._parsed.GetOrAdd((reference.Name, cached.Schema.Metadata.ResourceVersion, version.Version), _ => JsonSchema.FromText(serializer.SerializeToText(version.Schema)));
        return (parsed, version.Version, null);
    }

}
