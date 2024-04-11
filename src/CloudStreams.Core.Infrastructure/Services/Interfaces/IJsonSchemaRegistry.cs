using Json.Schema;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to store <see cref="JsonSchema"/>s
/// </summary>
public interface IJsonSchemaRegistry
{

    /// <summary>
    /// Gets the <see cref="JsonSchema"/> at the specified <see cref="Uri"/>
    /// </summary>
    /// <param name="uri">The <see cref="Uri"/> of the <see cref="JsonSchema"/> to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The <see cref="JsonSchema"/> at the specified <see cref="Uri"/></returns>
    Task<JsonSchema> GetAsync(Uri uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers the specified <see cref="JsonSchema"/>
    /// </summary>
    /// <param name="schema">The <see cref="JsonSchema"/> to register</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task RegisterAsync(JsonSchema schema, CancellationToken cancellationToken = default);

}
