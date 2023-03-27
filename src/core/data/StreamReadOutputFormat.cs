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
/// Enumerates all supported read output formats
/// </summary>
[TypeConverter(typeof(StringEnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]
public enum StreamReadOutputFormat
{
    /// <summary>
    /// Specifies that the stream should output cloud events
    /// </summary>
    [EnumMember(Value = "event")]
    Event = 1,
    /// <summary>
    /// Specifies that the stream should output cloud event records
    /// </summary>
    [EnumMember(Value = "record")]
    Record = 2
}