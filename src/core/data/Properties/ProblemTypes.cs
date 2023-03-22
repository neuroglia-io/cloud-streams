namespace CloudStreams.Core.Data.Properties;

/// <summary>
/// Exposes constants about problem types
/// </summary>
public static class ProblemTypes
{

    static readonly Uri BaseUri = new("https://cloud-streams.io/problems/");

    /// <summary>
    /// Gets the uri that reference a problem due to a resource not being modified as expected
    /// </summary>
    public static readonly Uri NotModified = new(BaseUri, "not-modified");
    /// <summary>
    /// Gets the uri that reference a problem due to failed validation
    /// </summary>
    public static readonly Uri ValidationFailed = new(BaseUri, "validation-failed");
    /// <summary>
    /// Gets the uri that reference a problem due to the failure to retrieve the definition of a resource
    /// </summary>
    public static readonly Uri ResourceDefinitionNotFound = new(BaseUri, "resources/definitions/not-found");
    /// <summary>
    /// Gets the uri that reference a problem due to the failure to retrieve a specific resource
    /// </summary>
    public static readonly Uri ResourceNotFound = new(BaseUri, "resources/not-found");
    /// <summary>
    /// Gets the uri that reference a problem that occured during a patch
    /// </summary>
    public static readonly Uri ResourcePatchFailed = new(BaseUri, "resources/patch-failed");

}
