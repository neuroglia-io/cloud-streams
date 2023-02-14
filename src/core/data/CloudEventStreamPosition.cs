namespace CloudStreams;

/// <summary>
/// Provides mechanisms to handle positioning/offsetting in cloud event streams
/// </summary>
public static class CloudEventStreamPosition
{

    /// <summary>
    /// Specifies the start of the stream
    /// </summary>
    public const long Start = 0;

    /// <summary>
    /// Specifies the end of the stream
    /// </summary>
    public const long End = -1;

}
