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
/// Enumerates all supported cloud event stream partition types
/// </summary>
[TypeConverter(typeof(StringEnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]
public enum CloudEventPartitionType
{
    /// <summary>
    /// Indicates a partition by cloud event source
    /// </summary>
    [EnumMember(Value = "by-source")]
    BySource = 1,
    /// <summary>
    /// Indicates a partition by cloud event type
    /// </summary>
    [EnumMember(Value = "by-type")]
    ByType = 2,
    /// <summary>
    /// Indicates a partition by subject
    /// </summary>
    [EnumMember(Value = "by-subject")]
    BySubject = 4
}