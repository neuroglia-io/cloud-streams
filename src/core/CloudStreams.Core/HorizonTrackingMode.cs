// Copyright © 2024-Present The Cloud Streams Authors
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

using Neuroglia.Serialization.Json.Converters;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all supported horizon tracking modes
/// </summary>
[TypeConverter(typeof(EnumMemberTypeConverter))]
[JsonConverter(typeof(StringEnumConverter))]
public enum HorizonTrackingMode
{
    /// <summary>
    /// Indicates that only the most recent origin is tracked
    /// </summary>
    [EnumMember(Value = "single")]
    Single = 1,
    /// <summary>
    /// Indicates that all gateways that have processed the event are tracked
    /// </summary>
    [EnumMember(Value = "chain")]
    Chain = 2
}
