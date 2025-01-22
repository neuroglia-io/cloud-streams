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
using System.Net;

namespace CloudStreams.Core;

/// <summary>
/// Enumerates all the supported actions that can be performed when a loop has been detected
/// </summary>
[TypeConverter(typeof(EnumMemberTypeConverter))]
[JsonConverter(typeof(StringEnumConverter))]
public enum LoopAction
{
    /// <summary>
    /// Indicates that the event should be ignored, and that the gateway should return a success status code (<see cref="HttpStatusCode.Accepted"/>), as if the ingestion occurred successfully
    /// </summary>
    [EnumMember(Value = "ignore")]
    Ignore = 1,
    /// <summary>
    /// Indicates that the event should be rejected, meaning that the gateway should return a non-success status code (<see cref="HttpStatusCode.Conflict"/>)
    /// </summary>
    [EnumMember(Value = "reject")]
    Reject = 2,
    /// <summary>
    /// Indicates that the event should be forwarded to the gateway's dead-letter queue
    /// </summary>
    [EnumMember(Value = "forward")]
    Forward = 4
}