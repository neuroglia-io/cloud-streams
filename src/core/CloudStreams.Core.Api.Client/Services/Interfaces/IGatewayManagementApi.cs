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

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of the API used to manage <see cref="Gateway"/>s
/// </summary>
public interface IGatewayManagementApi
    : IResourceManagementApi<Gateway>
{

    /// <summary>
    /// Evaluates a cloud event against the specified spec of the specified gateway, which may not be saved yet, without ingesting it
    /// </summary>
    /// <param name="name">The name of the gateway</param>
    /// <param name="request">An object that describes the spec to evaluate the cloud event against, and the cloud event</param>
    /// <param name="namespace">The namespace the gateway belongs to, if any. Defaults to the default namespace</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>What would become of the cloud event, and why</returns>
    Task<GatewayAdmissionTestResult> TestAdmissionAsync(string name, GatewayAdmissionTestRequest request, string? @namespace = null, CancellationToken cancellationToken = default);

}

/// <summary>
/// Defines the fundamentals of the API used to manage <see cref="Schema"/>s
/// </summary>
public interface ISchemaManagementApi
    : IResourceManagementApi<Schema>
{

    /// <summary>
    /// Checks whether the specified JSON schema would be accepted as the next version of the specified schema, without changing it
    /// </summary>
    /// <param name="name">The name of the schema</param>
    /// <param name="schema">The JSON schema to check</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>Whether the JSON schema is compatible with the latest version, and why it is not</returns>
    Task<SchemaCompatibilityResult> CheckCompatibilityAsync(string name, System.Text.Json.Nodes.JsonNode schema, CancellationToken cancellationToken = default);

}
