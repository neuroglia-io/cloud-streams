namespace CloudStreams.Data.Models;

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

    public static readonly ResourceType ResourceType = new(ResourceGroup, ResourceVersion, ResourcePlural);

    /// <inheritdoc/>
    public Channel() : base(ResourceType) { }

    /// <inheritdoc/>
    public Channel(ResourceMetadata metadata, ChannelSpec spec, ChannelStatus? status = null) : base(ResourceType, metadata, spec, status) { }

}
