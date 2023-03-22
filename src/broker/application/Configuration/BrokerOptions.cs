using CloudStreams.Core.Infrastructure.Configuration;

namespace CloudStreams.Broker.Application.Configuration;

/// <summary>
/// Represents the options used to configure a Cloud Streams cloud event gateway
/// </summary>
public class BrokerOptions
    : ResourceControllerOptions
{

    /// <summary>
    /// Gets the prefix for all Cloud Streams broker related environment variables
    /// </summary>
    public const string EnvironmentVariablePrefix = "CLOUDSTREAMS_BROKER_";

    /// <summary>
    /// Initializes a new <see cref="BrokerOptions"/>
    /// </summary>
    public BrokerOptions()
    {
        this.LabelSelectors = new()
        {

        };
    }

    /// <summary>
    /// Gets/sets the gateway's name
    /// </summary>
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the gateway's namespace
    /// </summary>
    public virtual string? Namespace { get; set; } = null!;

}
