namespace CloudStreams.Core;

/// <summary>
/// Enumerates all supported cloud event media types names
/// </summary>
public static class CloudEventMediaTypeNames
{

    /// <summary>
    /// Gets the 'application/cloudevents' media type, which assumes an encoding in JSON
    /// </summary>
    public const string CloudEvents = "application/cloudevents";
    /// <summary>
    /// Gets the 'application/cloudevents+json' media type
    /// </summary>
    public const string CloudEventsJson = CloudEvents + "+json";
    /// <summary>
    /// Gets the 'application/cloudevents+yaml' media type
    /// </summary>
    public const string CloudEventsYaml = CloudEvents + "+yaml";

}
