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

namespace CloudStreams.Core.Data.Properties;

/// <summary>
/// Exposes constants about problem types
/// </summary>
public static class ProblemTypes
{

    static readonly Uri BaseUri = new("https://cloud-streams.io/problems/");

    /// <summary>
    /// Gets the uri that reference a problem due to a resource not being modified as expected
    /// </summary>
    public static readonly Uri NotModified = new(BaseUri, "not-modified");
    /// <summary>
    /// Gets the uri that reference a problem due to failed validation
    /// </summary>
    public static readonly Uri ValidationFailed = new(BaseUri, "validation-failed");
    /// <summary>
    /// Gets the uri that reference a problem due to the failure to retrieve the definition of a resource
    /// </summary>
    public static readonly Uri ResourceDefinitionNotFound = new(BaseUri, "resources/definitions/not-found");
    /// <summary>
    /// Gets the uri that reference a problem due to the failure to retrieve a specific resource
    /// </summary>
    public static readonly Uri ResourceNotFound = new(BaseUri, "resources/not-found");
    /// <summary>
    /// Gets the uri that reference a problem that occured during a patch
    /// </summary>
    public static readonly Uri ResourcePatchFailed = new(BaseUri, "resources/patch-failed");

}
