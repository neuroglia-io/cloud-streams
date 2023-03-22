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
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Models;

/// <summary>
/// Enumerates all supported artifact states
/// </summary>
[TypeConverter(typeof(EnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]

public enum ArtifactState
{
    /// <summary>
    /// Indicates the the artifact is enabled
    /// </summary>
    [EnumMember(Value = "ENABLED")]
    Enabled,
    /// <summary>
    /// Indicates the the artifact is disabled
    /// </summary>
    [EnumMember(Value = "DISABLED")]
    Disabled,
    /// <summary>
    /// Indicates the the artifact has been deprecated
    /// </summary>
    [EnumMember(Value = "DEPRECATED")]
    Deprecated
}
