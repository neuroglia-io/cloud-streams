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
/// Enumerates all actions to undertake if an artifact already exist when attempting to create a new one
/// </summary>
[TypeConverter(typeof(StringEnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]
public enum IfArtifactExistsAction
{
    /// <summary>
    /// Server rejects the content with a 409 error if the artifact already exists
    /// </summary>
    [EnumMember(Value = "FAIL")]
    Fail,
    /// <summary>
    /// Server updates the existing artifact and returns the new metadata
    /// </summary>
    [EnumMember(Value = "UPDATE")]
    Update,
    /// <summary>
    /// Server does not create or add content to the server, but instead returns the metadata for the existing artifact
    /// </summary>
    [EnumMember(Value = "RETURN")]
    Return,
    /// <summary>
    /// Server returns an existing version that matches the provided content if such a version exists, otherwise a new version is created
    /// </summary>
    [EnumMember(Value = "RETURN_OR_UPDATE")]
    ReturnOrUpdate,
}
