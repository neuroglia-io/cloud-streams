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
/// Enumerates resoruce label selection operators 
/// </summary>
[TypeConverter(typeof(StringEnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]
public enum ResourceLabelSelectionOperator
{
    /// <summary>
    /// Indicates that the label value must be equal to the specified value
    /// </summary>
    [EnumMember(Value = "equals")]
    Equals,
    /// <summary>
    /// Indicates that the label value must not be equal to the specified value
    /// </summary>
    [EnumMember(Value = "not-equals")]
    NotEquals,
    /// <summary>
    /// Indicates that the resource must have the specified label. If values have been supplied, the label must also have one of the specified values
    /// </summary>
    [EnumMember(Value = "contains")]
    Contains,
    /// <summary>
    /// Indicates that the resource must not the specified label. If values have been supplied, the label must also have one of the specified values
    /// </summary>
    [EnumMember(Value = "not-contains")]
    NotContains
}