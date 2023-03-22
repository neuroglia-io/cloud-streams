using CloudStreams.Core.Data.Models;

namespace CloudStreams.ResourceManagement.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of a service used to interact with a Cloud Streams gateway's API
/// </summary>
public interface ICloudStreamsResourceManagementApiClient
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
    /// Gets the API used to manage <see cref="Network"/>s
    /// </summary>
    public IResourceManagementApi<Network> Networks { get; }

    /// <summary>
    /// Gets the API used to manage <see cref="Subscription"/>s
    /// </summary>
    public IResourceManagementApi<Subscription> Subscriptions { get; }

}