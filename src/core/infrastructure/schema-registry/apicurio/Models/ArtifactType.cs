using CloudStreams.Core.Serialization.Json.Converters;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace CloudStreams.Core.Infrastructure.SchemaRegistry.Apicurio.Models;

/// <summary>
/// Enumerates all supported artifact types
/// </summary>
[TypeConverter(typeof(EnumMemberConverter))]
[JsonConverter(typeof(StringEnumConverterFactory))]

public enum ArtifactType
{
    /// <summary>
    /// Indicates an Avro schema
    /// </summary>
    [EnumMember(Value = "Avro")]
    Avro,
    /// <summary>
    /// Indicates a PROTOBUF schema
    /// </summary>
    [EnumMember(Value = "PROTOBUF")]
    PROTOBUF,
    /// <summary>
    /// Indicates a JSON schema
    /// </summary>
    [EnumMember(Value = "JSON")]
    JSON,
    /// <summary>
    /// Indicates an OPENAPI schema
    /// </summary>
    [EnumMember(Value = "OPENAPI")]
    OPENAPI,
    /// <summary>
    /// Indicates an ASYNCAPI schema
    /// </summary>
    [EnumMember(Value = "ASYNCAPI")]
    ASYNCAPI,
    /// <summary>
    /// Indicates a GRAPHQL schema
    /// </summary>
    [EnumMember(Value = "GRAPHQL")]
    GRAPHQL,
    /// <summary>
    /// Indicates a KCONNECT schema
    /// </summary>
    [EnumMember(Value = "KCONNECT")]
    KCONNECT,
    /// <summary>
    /// Indicates a WSDL schema
    /// </summary>
    [EnumMember(Value = "WSDL")]
    WSDL,
    /// <summary>
    /// Indicates an XSD schema
    /// </summary>
    [EnumMember(Value = "XSD")]
    XSD,
    /// <summary>
    /// Indicates an XML schema
    /// </summary>
    [EnumMember(Value = "XML")]
    XML
}