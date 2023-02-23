namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents a cloud event subscription
/// </summary>
[DataContract]
public class Subscription
    : Resource<SubscriptionSpec, SubscriptionStatus>
{

    const string ResourceGroup = CloudStreamsDefaults.ResourceGroup;

    const string ResourceVersion = "v1";

    const string ResourcePlural = "subscriptions";

    const string ResourceKind = "Subscription";

    /// <summary>
    /// Gets the <see cref="Subscription"/>'s resource type
    /// </summary>
    public static readonly ResourceType ResourceType = new(ResourceGroup, ResourceVersion, ResourcePlural, ResourceKind);

    /// <inheritdoc/>
    public Subscription() : base(ResourceType) { }

    /// <inheritdoc/>
    public Subscription(ResourceMetadata metadata, SubscriptionSpec spec, SubscriptionStatus? status = null) : base(ResourceType, metadata, spec, status) { }

}
