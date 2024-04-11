using Microsoft.AspNetCore.SignalR;
using Neuroglia.Eventing.CloudEvents;

namespace CloudStreams.Gateway.Services;

/// <summary>
/// Represents the <see cref="Hub"/> used to observe <see cref="CloudEvent"/>s
/// </summary>
public class CloudEventHub
    : Hub<ICloudEventHubClient>, ICloudEventHub
{



}
