using CloudStreams.Core.Infrastructure.Configuration;

namespace CloudStreams.Channel.Application.Configuration;

/// <summary>
/// Represents the options used to configure a Cloud Streams cloud event channel
/// </summary>
public class ChannelOptions
    : ResourceControllerOptions
{

    /// <summary>
    /// Gets the prefix for all Cloud Streams channel related environment variables
    /// </summary>
    public const string EnvironmentVariablePrefix = "CLOUDSTREAMS_CHANNEL_";

    /// <summary>
    /// Initializes a new <see cref="ChannelOptions"/>
    /// </summary>
    public ChannelOptions()
    {
        this.LabelSelectors = new()
        {

        };
    }

    /// <summary>
    /// Gets/sets the channel's name
    /// </summary>
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the channel's namespace
    /// </summary>
    public virtual string? Namespace { get; set; } = null!;

}