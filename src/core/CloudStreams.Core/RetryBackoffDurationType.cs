﻿// Copyright © 2024-Present The Cloud Streams Authors
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
/// Enumerates all supported retry backoff duration types
/// </summary>
[TypeConverter(typeof(EnumMemberTypeConverter))]
[JsonConverter(typeof(StringEnumConverter))]
public enum RetryBackoffDurationType
{
    /// <summary>
    /// Indicates a constant duration
    /// </summary>
    [EnumMember(Value = "constant")]
    Constant = 1,
    /// <summary>
    /// Indicates a duration that increments at every retry attempt in a constant fashion
    /// </summary>
    [EnumMember(Value = "incremental")]
    Incremental = 2,
    /// <summary>
    /// Indicates an exponential duration
    /// </summary>
    [EnumMember(Value = "exponential")]
    Exponential = 4
}
