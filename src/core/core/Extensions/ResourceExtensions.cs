﻿namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="IResource"/>s
/// </summary>
public static class ResourceExtensions
{

    /// <summary>
    /// Gets the <see cref="IResource"/>'s name
    /// </summary>
    /// <param name="resource">The <see cref="IResource"/> to get the name of</param>
    /// <returns>The <see cref="IResource"/>'s name</returns>
    public static string GetName(this IResource resource) => resource.Metadata.Name;

    /// <summary>
    /// Gets the <see cref="IResource"/>'s namespace
    /// </summary>
    /// <param name="resource">The <see cref="IResource"/> to get the namespace of</param>
    /// <returns>The <see cref="IResource"/>'s namespace</returns>
    public static string? GetNamespace(this IResource resource) => resource.Metadata.Namespace;

    /// <summary>
    /// Determines whether or not the <see cref="IResource"/> is the specified type
    /// </summary>
    /// <param name="resource">The <see cref="IResource"/> to check</param>
    /// <param name="type">The expected type of <see cref="IResource"/></param>
    /// <returns>A boolean indicating whether or not the <see cref="IResource"/> is of the specified type</returns>
    public static bool IsOfType(this IResource resource, ResourceType type)
    {
        if (resource.Type != null) return resource.Type == type;
        if (type == null) throw new ArgumentNullException(nameof(type));
        return resource.ApiVersion == type.GetApiVersion() && resource.Kind == type.Kind;
    }

    /// <summary>
    /// Determines whether or not the <see cref="IResource"/> is the specified type
    /// </summary>
    /// <typeparam name="TResource">The expected type of <see cref="IResource"/></typeparam>
    /// <param name="resource">The <see cref="IResource"/> to check</param>
    /// <returns>A boolean indicating whether or not the <see cref="IResource"/> is of the specified type</returns>
    public static bool IsOfType<TResource>(this IResource resource)
        where TResource : class, IResource, new()
    {
        return resource.IsOfType(new TResource().Type);
    }

}