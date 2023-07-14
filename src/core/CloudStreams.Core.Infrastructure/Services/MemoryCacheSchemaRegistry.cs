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

using Hylo;
using Microsoft.Extensions.Caching.Memory;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the default <see cref="IMemoryCache"/> based implementation of the <see cref="ISchemaRegistry"/> interface
/// </summary>
public class MemoryCacheSchemaRegistry
    : ISchemaRegistry
{

    /// <summary>
    /// Initializes a new <see cref="MemoryCacheSchemaRegistry"/>
    /// </summary>
    /// <param name="memoryCache">The current <see cref="IMemoryCache"/></param>
    public MemoryCacheSchemaRegistry(IMemoryCache memoryCache)
    {
        this.MemoryCache = memoryCache;
    }

    /// <summary>
    /// Gets the current <see cref="IMemoryCache"/>
    /// </summary>
    protected IMemoryCache MemoryCache { get; }

    /// <inheritdoc/>
    public virtual Task<Uri> RegisterSchemaAsync(JsonSchema schema, CancellationToken cancellationToken = default)
    {
        if(schema == null) throw new ArgumentNullException(nameof(schema));
        var id = schema.Keywords?.OfType<IdKeyword>().FirstOrDefault()?.Id.OriginalString;
        if (!string.IsNullOrWhiteSpace(id)) id = Guid.NewGuid().ToString();
        var uri = schema.BaseUri ?? new Uri($"https://cloud-streams.io/schemas/{id!.ToHyphenCase()}", UriKind.Absolute);
        this.MemoryCache.Set(uri, schema);
        this.MemoryCache.Set(id!, schema);
        return Task.FromResult(uri);
    }

    /// <inheritdoc/>
    public virtual Task<JsonSchema?> GetSchemaAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (uri == null) throw new ArgumentNullException(nameof(uri));
        this.MemoryCache.TryGetValue(uri.OriginalString, out JsonSchema? schema);
        return Task.FromResult(schema);
    }

    /// <inheritdoc/>
    public virtual Task<Uri?> GetSchemaUriByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
        if (!this.MemoryCache.TryGetValue(id, out JsonSchema? schema) || schema == null) return Task.FromResult((Uri?)null);
        var uri = schema.BaseUri ?? new Uri($"https://cloud-streams.io/schemas/{id!.ToHyphenCase()}", UriKind.Absolute);
        return Task.FromResult(uri)!;
    }

}
