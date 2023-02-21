namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents a cloud event broker
/// </summary>
[DataContract]
public class Broker
    : Resource<BrokerSpec, BrokerStatus>
{

    const string ResourceGroup = CloudStreamsDefaults.ResourceGroup;

    const string ResourceVersion = "v1";

    const string ResourcePlural = "brokers";

    const string ResourceKind = "Broker";

    /// <summary>
    /// Gets the <see cref="Broker"/>'s resource type
    /// </summary>
    public static readonly ResourceType ResourceType = new(ResourceGroup, ResourceVersion, ResourcePlural, ResourceKind);

    /// <inheritdoc/>
    public Broker() : base(ResourceType) { }

    /// <inheritdoc/>
    public Broker(ResourceMetadata metadata, BrokerSpec spec, BrokerStatus? status = null) : base(ResourceType, metadata, spec, status) { }

}
