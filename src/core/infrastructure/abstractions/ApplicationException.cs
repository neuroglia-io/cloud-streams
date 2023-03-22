using CloudStreams.Core.Data.Properties;

namespace CloudStreams.Core;

/// <summary>
/// Represents an <see cref="Exception"/> produced by a CloudStreams app
/// </summary>
public class ApplicationException
    : Exception
{

    /// <summary>
    /// Initializes a new <see cref="ApplicationException"/>
    /// </summary>
    /// <param name="title">The <see cref="ApplicationException"/>'s title</param>
    /// <param name="detail">The <see cref="ApplicationException"/>'s detail</param>
    /// <param name="innerException">The <see cref="ApplicationException"/>'s inner <see cref="Exception"/></param>
    public ApplicationException(string title, string detail, Exception? innerException = null) 
        : base(detail, innerException) 
    {
        this.Title = title;
    }

    /// <summary>
    /// Gets the <see cref="ApplicationException"/>'s title
    /// </summary>
    protected string Title { get; }

    /// <summary>
    /// Creates a new <see cref="ApplicationException"/> thrown when the application failed to find an <see cref="IResource"/>
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> that could not be found</typeparam>
    /// <param name="name">The name of the <see cref="IResource"/> that could not be found</param>
    /// <param name="namespace">The namespace the <see cref="IResource"/> that could not be found belongs to</param>
    /// <returns>A new <see cref="ApplicationException"/></returns>
    public static ApplicationException ResourceNotFound<TResource>(string name, string? @namespace) 
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        var details = string.IsNullOrWhiteSpace(@namespace) ? 
            StringExtensions.Format(Core.Data.Properties.ProblemDetails.ClusterResourceNotFound, resource.Type.Group, resource.Type.Version, resource.Type.Plural, name) 
            : StringExtensions.Format(Core.Data.Properties.ProblemDetails.NamespacedResourceNotFound, resource.Type.Group, resource.Type.Version, resource.Type.Plural, @namespace, name);
        return new(ProblemTitles.NotFound, details);
    }

}
