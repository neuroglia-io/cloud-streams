namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="IResourceReference"/>s
/// </summary>
public static class IResourceReferenceExtensions
{

    /// <summary>
    /// Gets the group the referenced <see cref="IResource"/> belongs to
    /// </summary>
    /// <param name="reference">The extended <see cref="IResourceReference"/></param>
    /// <returns>The group the referenced <see cref="IResource"/> belongs to</returns>
    public static string GetGroup(this IResourceReference reference) => reference.ApiVersion.Split('/')[0];

    /// <summary>
    /// Gets the referenced <see cref="IResource"/>'s version
    /// </summary>
    /// <param name="reference">The extended <see cref="IResourceReference"/></param>
    /// <returns>The referenced <see cref="IResource"/>'s version</returns>
    public static string GetVersion(this IResourceReference reference) => reference.ApiVersion.Split('/')[1];

    /// <summary>
    /// Determines whether or not the <see cref="IResourceReference"/> refers to a namespaced <see cref="IResource"/>
    /// </summary>
    /// <param name="reference">The <see cref="IResourceReference"/> to check</param>
    /// <returns>A boolean indicating whether or not the <see cref="IResourceReference"/> refers to a namespaced <see cref="IResource"/></returns>
    public static bool IsNamespaced(this IResourceReference reference) => !string.IsNullOrWhiteSpace(reference.Namespace);

}