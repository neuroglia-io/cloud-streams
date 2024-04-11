using Json.Schema;

namespace CloudStreams.Gateway.Application.Configuration;

/// <summary>
/// Represents an object used to configure how to generate <see cref="JsonSchema"/>s
/// </summary>
public class JsonSchemaGenerationOptions
{

    /// <summary>
    /// Gets/sets the id of the <see cref="JsonSchema"/> to generate
    /// </summary>
    public virtual string? Id { get; set; }

    /// <summary>
    /// Gets/sets the title of the <see cref="JsonSchema"/> to generate
    /// </summary>
    public virtual string? Title { get; set; }

}
