namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents a cloud event gateway, reponsible for authorizing, validating and persisting inbound events
/// </summary>
[DataContract]
public class Gateway
    : Resource<GatewaySpec>
{

    const string ResourceGroup = CloudStreamsDefaults.ResourceGroup;

    const string ResourceVersion = "v1";

    const string ResourcePlural = "gateways";

    const string ResourceKind = "Gateway";

    /// <summary>
    /// Gets the <see cref="Gateway"/>'s resource type
    /// </summary>
    public static readonly ResourceType ResourceType = new(ResourceGroup, ResourceVersion, ResourcePlural, ResourceKind);

    /// <inheritdoc/>
    public Gateway() : base(ResourceType) { }

    /// <inheritdoc/>
    public Gateway(ResourceMetadata metadata, GatewaySpec spec) : base(ResourceType, metadata, spec) { }

}
