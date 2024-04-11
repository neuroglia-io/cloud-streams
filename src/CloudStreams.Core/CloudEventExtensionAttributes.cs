namespace CloudStreams.Core;

/// <summary>
/// Enumerates all cloud event extension attributes used by Cloud Streams
/// </summary>
public static class CloudEventExtensionAttributes
{

    /// <summary>
    /// Gets the name of the cloud event attribute used to store its sequence on the stream it belongs to
    /// </summary>
    public const string Sequence = "sequence";

}
