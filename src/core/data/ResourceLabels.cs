namespace CloudStreams.Core;

/// <summary>
/// Exposes labels for Cloud Streams resources
/// </summary>
public static class ResourceLabels
{

    /// <summary>
    /// Gets the prefix of resource labels
    /// </summary>
    public const string Prefix = "io.cloud-streams/";
    /// <summary>
    /// Gets the network resource label
    /// </summary>
    public const string NetworkId = Prefix + "network-id";

}