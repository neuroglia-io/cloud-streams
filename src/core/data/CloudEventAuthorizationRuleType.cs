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
/// Enumerates default cloud event authorization rule types
/// </summary>
[TypeConverter(typeof(StringEnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]
public enum CloudEventAuthorizationRuleType
{
    /// <summary>
    /// Indicates a policy that performs checks on cloud event context attributes
    /// </summary>
    [EnumMember(Value = "attribute")]
    Attribute = 1,
    /// <summary>
    /// Indicates a policy that performs checks on cloud event payloads
    /// </summary>
    [EnumMember(Value = "payload")]
    Payload = 2,
    /// <summary>
    /// Indicates a policy that grants or refuses accesss based on the time of day
    /// </summary>
    [EnumMember(Value = "timeOfDay")]
    TimeOfDay = 4,
    /// <summary>
    /// Indicates a policy that grants or refuses access over a given period of time
    /// </summary>
    [EnumMember(Value = "temporary")]
    Temporary = 8
}