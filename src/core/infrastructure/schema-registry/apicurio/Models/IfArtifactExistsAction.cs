using CloudStreams.Core.Serialization.Json.Converters;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Models;

/// <summary>
/// Enumerates all actions to undertake if an artifact already exist when attempting to create a new one
/// </summary>
[TypeConverter(typeof(EnumMemberConverter))]
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
