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

using CloudStreams.Core.Serialization.Json.Converters;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all supported patch types
/// </summary>
[TypeConverter(typeof(EnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]
public enum PatchType
{
    /// <summary>
    /// Indicates a <see href="https://www.rfc-editor.org/rfc/rfc6902">Json Patch</see>
    /// </summary>
    [EnumMember(Value = "patch")]
    JsonPatch,
    /// <summary>
    /// Indicates a <see href="https://www.rfc-editor.org/rfc/rfc7386">Json Merge Patch</see>
    /// </summary>
    [EnumMember(Value = "merge")]
    JsonMergePatch,
    /// <summary>
    /// Indicates a <see href="https://github.com/kubernetes/community/blob/master/contributors/devel/sig-api-machinery/strategic-merge-patch.md">Json Strategic Merge Patch</see>
    /// </summary>
    [EnumMember(Value = "strategic")]
    StrategicMergePatch
}