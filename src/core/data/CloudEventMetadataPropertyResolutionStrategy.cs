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
/// Enumerates all strategies for resolving cloud event metadata properties
/// </summary>
[TypeConverter(typeof(StringEnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]
public enum CloudEventMetadataPropertyResolutionStrategy
{
    /// <summary>
    /// Indicates that the metadata property is extracted from a context attribute
    /// </summary>
    [EnumMember(Value = "attribute")]
    ContextAttribute,
    /// <summary>
    /// Indicates that the metadata property is extracted by evaluating a runtime expression against the event
    /// </summary>
    [EnumMember(Value = "expression")]
    RuntimeExpression
}