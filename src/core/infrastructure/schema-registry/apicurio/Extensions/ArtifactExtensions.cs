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

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="Artifact"/>s
/// </summary>
public static class ArtifactExtensions
{

    /// <summary>
    /// Gets the path to the <see cref="Artifact"/>
    /// </summary>
    /// <param name="artifact">The <see cref="Artifact"/>'s path</param>
    /// <returns>The <see cref="Artifact"/>'s path</returns>
    public static string GetPath(this Artifact artifact) => $"apis/registry/v2/groups/{artifact.GroupId}/artifacts/{artifact.ContentId}/versions/{artifact.Version}";

}
