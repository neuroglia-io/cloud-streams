using CloudStreams.Gateway.Configuration;
using Json.Schema;

namespace CloudStreams.Gateway.Services;

/// <summary>
/// Defines the fundamentals of a service used to generate <see cref="JsonSchema"/>s
/// </summary>
public interface IJsonSchemaGenerator
{

    /// <summary>
    /// Generates a new <see cref="JsonSchema"/> based on the specified graph
    /// </summary>
    /// <param name="graph">The object to generate a new <see cref="JsonSchema"/> for</param>
    /// <param name="options">The <see cref="JsonSchemaGenerationOptions"/> to use</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="JsonSchema"/></returns>
    Task<JsonSchema?> GenerateAsync(object? graph, JsonSchemaGenerationOptions? options = null, CancellationToken cancellationToken = default);

}