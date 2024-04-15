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

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Defines the fundamentals of a service used to store <see cref="JsonSchema"/>s
/// </summary>
public interface IJsonSchemaRegistry
{

    /// <summary>
    /// Gets the <see cref="JsonSchema"/> at the specified <see cref="Uri"/>
    /// </summary>
    /// <param name="uri">The <see cref="Uri"/> of the <see cref="JsonSchema"/> to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The <see cref="JsonSchema"/> at the specified <see cref="Uri"/></returns>
    Task<JsonSchema> GetAsync(Uri uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers the specified <see cref="JsonSchema"/>
    /// </summary>
    /// <param name="schema">The <see cref="JsonSchema"/> to register</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task RegisterAsync(JsonSchema schema, CancellationToken cancellationToken = default);

}
