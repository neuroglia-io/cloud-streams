namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents a cloud event subscription
/// </summary>
[DataContract]
public record Subscription
    : Resource<SubscriptionSpec, SubscriptionStatus>
{

    /// <summary>
    /// Gets the <see cref="Subscription"/>'s resource type
    /// </summary>
    public static readonly ResourceDefinitionInfo ResourceDefinition = new SubscriptionResourceDefinition()!;

    /// <inheritdoc/>
    public Subscription() : base(ResourceDefinition) { }

    /// <inheritdoc/>
    public Subscription(ResourceMetadata metadata, SubscriptionSpec spec, SubscriptionStatus? status = null) : base(ResourceDefinition, metadata, spec, status) { }

}
