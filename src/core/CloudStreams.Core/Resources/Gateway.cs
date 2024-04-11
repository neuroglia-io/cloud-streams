namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents a cloud event gateway, responsible for authorizing, validating and persisting inbound events
/// </summary>
[DataContract]
public record Gateway
    : Resource<GatewaySpec, GatewayStatus>
{

    /// <summary>
    /// Gets the <see cref="Gateway"/>'s resource type
    /// </summary>
    public static readonly ResourceDefinitionInfo ResourceDefinition = new GatewayResourceDefinition()!;

    /// <inheritdoc/>
    public Gateway() : base(ResourceDefinition) { }

    /// <inheritdoc/>
    public Gateway(ResourceMetadata metadata, GatewaySpec spec) : base(ResourceDefinition, metadata, spec) { }

}
