namespace CloudStreams;

/// <summary>
/// Represents an <see cref="Exception"/> thrown by a Cloud Streams application
/// </summary>
public class CloudStreamsException
    : Exception
{

    /// <summary>
    /// Initializes a new <see cref="CloudStreamsException"/>
    /// </summary>
    /// <param name="problemDetails">An object used to describe a problem that has occured on the CloudStreams API</param>
    public CloudStreamsException(Core.Data.Models.ProblemDetails? problemDetails = null)
    {
        this.ProblemDetails = problemDetails;
    }

    /// <summary>
    /// Gets an object used to describe a problem that has occured on the CloudStreams API
    /// </summary>
    public Core.Data.Models.ProblemDetails? ProblemDetails { get; }

}