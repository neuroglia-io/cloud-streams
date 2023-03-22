namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a schema registry
/// </summary>
public interface ISchemaRegistry
{

    /// <summary>
    /// Registers the specified <see cref="JsonSchema"/>
    /// </summary>
    /// <param name="schema">The <see cref="JsonSchema"/> to register</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The <see cref="Uri"/> referencing the registered <see cref="JsonSchema"/></returns>
    Task<Uri> RegisterSchemaAsync(JsonSchema schema, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the <see cref="JsonSchema"/> at the specified <see cref="Uri"/>
    /// </summary>
    /// <param name="uri">The <see cref="Uri"/> that references the <see cref="JsonSchema"/> to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The <see cref="JsonSchema"/> at the specified <see cref="Uri"/></returns>
    Task<JsonSchema?> GetSchemaAsync(Uri uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the <see cref="Uri"/> of the <see cref="JsonSchema"/> with the specified id
    /// </summary>
    /// <param name="id">The id of the <see cref="JsonSchema"/> to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The <see cref="Uri"/> of the <see cref="JsonSchema"/> with the specified id, if any</returns>
    Task<Uri?> GetSchemaUriByIdAsync(string id, CancellationToken cancellationToken = default);

}
