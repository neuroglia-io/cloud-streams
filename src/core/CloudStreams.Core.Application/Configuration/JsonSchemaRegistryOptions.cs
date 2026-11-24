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

namespace CloudStreams.Core.Application.Configuration;

/// <summary>
/// Represents the options used to configure how JSON schemas referenced by cloud events are fetched
/// </summary>
public class JsonSchemaRegistryOptions
{

    /// <summary>
    /// Gets the name of the configuration section the options are bound to
    /// </summary>
    public const string ConfigurationSectionName = "SchemaRegistry";

    /// <summary>
    /// Gets/sets the hosts schemas may be fetched from. Entries are host names (e.g. <c>schemas.example.com</c>) or wildcards (e.g. <c>*.example.com</c>).
    /// If empty, schemas may be fetched from any host that does not resolve to a private address
    /// </summary>
    public virtual List<string> AllowedHosts { get; set; } = [];

    /// <summary>
    /// Gets/sets a boolean indicating whether or not schemas may be fetched from private, loopback and link-local addresses. Defaults to false
    /// </summary>
    public virtual bool AllowPrivateNetworks { get; set; }

    /// <summary>
    /// Gets/sets the maximum amount of time to wait for a schema to be fetched. Defaults to 10 seconds
    /// </summary>
    public virtual TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets/sets the maximum size, in bytes, of a schema. Defaults to 1 MiB
    /// </summary>
    public virtual long MaxSize { get; set; } = 1024 * 1024;

    /// <summary>
    /// Gets/sets the maximum number of schemas to keep in cache. Defaults to 1000
    /// </summary>
    public virtual int CacheSize { get; set; } = 1000;

}
