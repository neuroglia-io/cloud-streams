namespace CloudStreams.Core.Api.Client.Services;

public partial class CloudStreamsCoreApiClient
    : IResourceManagementApiClient
{

    /// <summary>
    /// Gets the relative path to the Cloud Streams resource management API
    /// </summary>
    public const string ResourceManagementApiPath = ApiVersionPathPrefix + "resources/";

    /// <inheritdoc/>
    public IResourceManagementApi<Gateway> Gateways { get; private set; } = null!;

    /// <inheritdoc/>
    public IResourceManagementApi<Broker> Brokers { get; private set; } = null!;

    /// <inheritdoc/>
    public IResourceManagementApi<Subscription> Subscriptions { get; private set; } = null!;

}
