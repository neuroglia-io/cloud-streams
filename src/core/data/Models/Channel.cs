namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents a channel
/// </summary>
[DataContract]
public class Channel
    : Resource<ChannelSpec, ChannelStatus>
{

    const string ResourceGroup = CloudStreamsDefaults.ResourceGroup;

    const string ResourceVersion = "v1";

    const string ResourcePlural = "channels";

    const string ResourceKind = "Channel";

    /// <summary>
    /// Gets the <see cref="Channel"/>'s resource type
    /// </summary>
    public static readonly ResourceType ResourceType = new(ResourceGroup, ResourceVersion, ResourcePlural, ResourceKind);

    /// <inheritdoc/>
    public Channel() : base(ResourceType) { }

    /// <inheritdoc/>
    public Channel(ResourceMetadata metadata, ChannelSpec spec, ChannelStatus? status = null) : base(ResourceType, metadata, spec, status) { }

}
