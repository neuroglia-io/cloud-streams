﻿// Copyright © 2023-Present The Cloud Streams Authors
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
/// Defines the fundamentals of the Apicurio Registry metadata API
/// </summary>
public interface IMetadataApi
{

    /// <summary>
    /// Gets the metadata of the <see cref="Artifact"/> with the specified id
    /// </summary>
    /// <param name="artifactId">The id of the <see cref="Artifact"/> to get the metadata of</param>
    /// <param name="groupId">The id of the group the <see cref="Artifact"/> to get the metadata of belongs to</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The metadata of the <see cref="Artifact"/> with the specified id</returns>
    Task<Artifact> GetArtifactMetadataAsync(string artifactId, string groupId, CancellationToken cancellationToken = default);

}
