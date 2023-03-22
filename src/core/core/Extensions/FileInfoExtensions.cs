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

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="FileInfo"/>s
/// </summary>
public static class FileInfoExtensions
{

    /// <summary>
    /// Determines whether or not the <see cref="FileInfo"/> is a JSON file
    /// </summary>
    /// <param name="file">The <see cref="FileInfo"/> to check</param>
    /// <returns>A boolean indicating whether or not the <see cref="FileInfo"/> is a JSON file</returns>
    public static bool IsJson(this FileInfo file) => file.Extension.EndsWith(".json");

    /// <summary>
    /// Determines whether or not the <see cref="FileInfo"/> is a YAML file
    /// </summary>
    /// <param name="file">The <see cref="FileInfo"/> to check</param>
    /// <returns>A boolean indicating whether or not the <see cref="FileInfo"/> is a YAML file</returns>
    public static bool IsYaml(this FileInfo file) => file.Extension.EndsWith(".yaml") || file.Extension.EndsWith(".yml");

}
