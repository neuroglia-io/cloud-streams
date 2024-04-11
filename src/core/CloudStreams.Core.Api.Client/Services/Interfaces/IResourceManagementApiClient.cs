namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of the Cloud Streams API dedicated to resource management
/// </summary>
public interface IResourceManagementApiClient
{

    /// <summary>
    /// Gets the API used to manage <see cref="Gateway"/>s
    /// </summary>
    public IResourceManagementApi<Gateway> Gateways { get; }

    /// <summary>
    /// Gets the API used to manage <see cref="Broker"/>s
    /// </summary>
    public IResourceManagementApi<Broker> Brokers { get; }

    /// <summary>
    /// Gets the API used to manage <see cref="Subscription"/>s
    /// </summary>
    public IResourceManagementApi<Subscription> Subscriptions { get; }

}