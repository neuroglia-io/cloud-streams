namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents a cloud event network
/// </summary>
[DataContract]
public class Network
    : Resource<NetworkSpec, NetworkStatus>
{

    const string ResourceGroup = CloudStreamsDefaults.ResourceGroup;

    const string ResourceVersion = "v1";

    const string ResourcePlural = "networks";

    const string ResourceKind = "Network";

    /// <summary>
    /// Gets the <see cref="Network"/>'s resource type
    /// </summary>
    public static readonly ResourceType ResourceType = new(ResourceGroup, ResourceVersion, ResourcePlural, ResourceKind);

    /// <inheritdoc/>
    public Network() : base(ResourceType) { }

    /// <inheritdoc/>
    public Network(ResourceMetadata metadata, NetworkSpec spec, NetworkStatus? status = null) : base(ResourceType, metadata, spec, status) { }

}
