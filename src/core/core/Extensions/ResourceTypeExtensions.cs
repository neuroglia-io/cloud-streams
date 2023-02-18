namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="ResourceType"/>s
/// </summary>
public static class ResourceTypeExtensions
{

    /// <summary>
    /// Gets the <see cref="ResourceType"/>'s API version
    /// </summary>
    /// <param name="resourceType">The <see cref="ResourceType"/> to get the API version of</param>
    /// <returns>The <see cref="ResourceType"/>'s API version</returns>
    public static string GetApiVersion(this ResourceType resourceType) => $"{resourceType.Group}/{resourceType.Version}";

}
