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

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Configuration;

/// <summary>
/// Enumerates all supported line ending formats
/// </summary>
[TypeConverter(typeof(EnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]

public enum LineEndingFormatMode
{
    /// <summary>
    /// Indicates that original line endings should be preserved 
    /// </summary>
    [EnumMember(Value = "preserve")]
    Preserve,
    /// <summary>
    /// Indicates that original line endings should be converted to Unix line endings ('\n' character)
    /// </summary>
    [EnumMember(Value = "unix")]
    ConvertToUnix,
    /// <summary>
    /// Indicates that original line endings should be converted to Windows line endings ('\r\n' character)
    /// </summary>
    [EnumMember(Value = "win")]
    ConvertToWindows
}
