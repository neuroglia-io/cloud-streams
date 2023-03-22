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

using CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Models;

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Services;

/// <summary>
/// Defines the fundamentals of the Apicurio Registry artifacts API
/// </summary>
public interface IArtifactsApi
{

    /// <summary>
    /// Creates a new artifact by posting the <see cref="Artifact"/> content. The body of the request should be the raw content of the <see cref="Artifact"/>. This is typically in JSON format for most of the supported types, but may be in another format for a few (for example, PROTOBUF).
    /// </summary>
    /// <param name="groupId">The unique ID of an <see cref="Artifact"/> group.</param>
    /// <param name="artifactType">The type of the <see cref="Artifact"/> to create</param>
    /// <param name="artifactId">A globally unique identifier for the <see cref="Artifact"/> to create</param>
    /// <param name="content">The content of the <see cref="Artifact"/> to create</param>
    /// <param name="ifExists">The option used to instruct the server on what to do if the <see cref="Artifact"/> already exists.</param>
    /// <param name="canonical">Used only when the ifExists query parameter is set to RETURN_OR_UPDATE, this parameter can be set to true to indicate that the server should "canonicalize" the content when searching for a matching version. 
    /// The canonicalization algorithm is unique to each <see cref="Artifact"/> type, but typically involves removing extra whitespace and formatting the content in a consistent manner.</param>
    /// <param name="version">The version number of this initial version of the <see cref="Artifact"/> content. This would typically be a simple integer or a SemVer value. If not provided, the server will assign a version number automatically (starting with version 1).</param>
    /// <param name="description">The description of <see cref="Artifact"/> being added. Description must be ASCII-only string. If this is not provided, the server will extract the description from the <see cref="Artifact"/> content.</param>
    /// <param name="name">The name of <see cref="Artifact"/> being added. Name must be ASCII-only string. If this is not provided, the server will extract the name from the <see cref="Artifact"/> content</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The newly created <see cref="Artifact"/></returns>
    Task<Artifact> CreateArtifactAsync(ArtifactType artifactType, string content, IfArtifactExistsAction ifExists, string? artifactId = null, string groupId = "default", bool canonical = false, string? version = null, string? name = null, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the content for an <see cref="Artifact"/> version in the registry using the unique content identifier for that content. This content ID may be shared by multiple artifact versions in the case where the <see cref="Artifact"/> versions are identical.
    /// </summary>
    /// <param name="contentId">The global identifier of the <see cref="Artifact"/> content to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The <see cref="Artifact"/> version with the specified global id</returns>
    Task<string?> GetArtifactContentByIdAsync(long contentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest version of the <see cref="Artifact"/> in its raw form. The Content-Type of the response depends on the <see cref="Artifact"/> type. In most cases, this is application/json, but for some types it may be different (for example, PROTOBUF).
    /// </summary>
    /// <param name="artifactId">The id of the <see cref="Artifact"/> to get</param>
    /// <param name="groupId">The id of the group of the <see cref="Artifact"/> to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The latest version of the specified <see cref="Artifact"/></returns>
    Task<string> GetLatestArtifactAsync(string artifactId, string groupId, CancellationToken cancellationToken = default);

}
