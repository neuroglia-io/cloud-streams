namespace CloudStreams.Gateway.Configuration;

/// <summary>
/// Represents the options used to configure a Cloud Streams cloud event gateway
/// </summary>
public class GatewayOptions
{

    /// <summary>
    /// Gets the prefix for all Cloud Streams gateway related environment variables
    /// </summary>
    public const string EnvironmentVariablePrefix = "CLOUDSTREAMS_GATEWAY_";

    /// <summary>
    /// Gets/sets the gateway's name
    /// </summary>
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the gateway's namespace
    /// </summary>
    public virtual string? Namespace { get; set; } = null!;

}
