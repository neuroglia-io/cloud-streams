namespace CloudStreams.Infrastructure.Services;

/// <summary>
/// Represents the default implementation of the <see cref="ISchemaGenerator"/> interface
/// </summary>
public class SchemaGenerator
    : ISchemaGenerator
{

    /// <inheritdoc/>
    public virtual async Task<JsonSchema> GenerateAsync(object graph, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(); //todo
    }

}