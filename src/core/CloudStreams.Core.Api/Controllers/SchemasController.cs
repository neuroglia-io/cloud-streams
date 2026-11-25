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

using CloudStreams.Core.Application.Queries.Resources.Generic;
using CloudStreams.Core.Application.Services;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the <see cref="ResourceApiController{TResource}"/> used to manage the registered JSON <see cref="Schema"/>s (see #213)
/// </summary>
/// <param name="mediator">The service used to mediate calls</param>
/// <param name="serializer">The service used to serialize the JSON schemas of versions</param>
[Route("api/resources/v1/schemas")]
public class SchemasController(IMediator mediator, IJsonSerializer serializer)
    : ClusterResourceApiController<Schema>(mediator)
{

    /// <summary>
    /// Gets the JSON schema of the specified version of the specified schema
    /// </summary>
    /// <param name="name">The name of the schema</param>
    /// <param name="version">The version to get, or 'latest'</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body is the JSON schema of the version</returns>
    [HttpGet("{name}/versions/{version}")]
    [Produces("application/schema+json", "application/json")]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> GetSchemaVersion(string name, string version, CancellationToken cancellationToken = default)
    {
        int? number = null;
        if (!string.Equals(version, "latest", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(version, out var parsed)) return NotFound(name, version);
            number = parsed;
        }
        var result = await this.Mediator.ExecuteAsync(new GetResourceQuery<Schema>(name, null), cancellationToken).ConfigureAwait(false);
        if (result.Status is < 200 or >= 300 || result.Data == null) return this.Process(result);
        var schemaVersion = result.Data.Spec.GetVersion(number);
        if (schemaVersion == null) return NotFound(name, version);
        return new ContentResult() { Content = serializer.SerializeToText(schemaVersion.Schema), ContentType = "application/schema+json", StatusCode = (int)HttpStatusCode.OK };
    }

    /// <summary>
    /// Checks whether the specified JSON schema would be accepted as the next version of the specified schema, without changing it
    /// </summary>
    /// <param name="name">The name of the schema</param>
    /// <param name="candidate">The JSON schema to check</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/> whose body describes whether the candidate is compatible with the latest version, and why it is not</returns>
    /// <remarks>Only requires the reader role, since it changes nothing</remarks>
    [HttpPost("{name}/compatibility")]
    [Authorize(Policy = CloudStreamsAuthenticationDefaults.ReadPolicy)]
    [ProducesResponseType(typeof(SchemaCompatibilityResult), (int)HttpStatusCode.OK)]
    [ProducesErrorResponseType(typeof(Neuroglia.ProblemDetails))]
    public virtual async Task<IActionResult> CheckCompatibility(string name, [FromBody] JsonNode candidate, CancellationToken cancellationToken = default)
    {
        var result = await this.Mediator.ExecuteAsync(new GetResourceQuery<Schema>(name, null), cancellationToken).ConfigureAwait(false);
        if (result.Status is < 200 or >= 300 || result.Data == null) return this.Process(result);
        var latest = result.Data.Spec.GetVersion();
        var compatibility = result.Data.Spec.GetCompatibility();
        var errors = latest == null ? [] : SchemaCompatibilityChecker.Check(serializer.SerializeToNode(latest.Schema), candidate, compatibility);
        return this.Ok(new SchemaCompatibilityResult() { Compatible = errors.Count == 0, Compatibility = compatibility, Version = latest?.Version, Errors = [.. errors] });
    }

    static ObjectResult NotFound(string name, string version) => new(new Neuroglia.ProblemDetails(ProblemTypes.NotFound, "Not Found", (int)HttpStatusCode.NotFound, $"The schema '{name}' has no version '{version}'"))
    {
        StatusCode = (int)HttpStatusCode.NotFound,
        ContentTypes = { "application/problem+json" }
    };

}
