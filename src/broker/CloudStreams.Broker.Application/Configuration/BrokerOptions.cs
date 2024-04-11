namespace CloudStreams.Broker.Application.Configuration;

/// <summary>
/// Represents the options used to configure a Cloud Streams cloud event broker
/// </summary>
public class BrokerOptions
{

    /// <summary>
    /// Gets the prefix for all Cloud Streams broker related environment variables
    /// </summary>
    public const string EnvironmentVariablePrefix = "CLOUDSTREAMS_BROKER_";

    /// <summary>
    /// Gets/sets the broker's name
    /// </summary>
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the broker's namespace
    /// </summary>
    public virtual string? Namespace { get; set; } = null!;

}
