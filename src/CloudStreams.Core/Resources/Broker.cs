namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents a cloud event broker
/// </summary>
[DataContract]
public record Broker
    : Resource<BrokerSpec, BrokerStatus>
{

    /// <summary>
    /// Gets the <see cref="Broker"/>'s resource type
    /// </summary>
    public static readonly ResourceDefinitionInfo ResourceDefinition = new BrokerResourceDefinition()!;

    /// <inheritdoc/>
    public Broker() : base(ResourceDefinition) { }

    /// <inheritdoc/>
    public Broker(ResourceMetadata metadata, BrokerSpec spec, BrokerStatus? status = null) : base(ResourceDefinition, metadata, spec, status) { }

}
