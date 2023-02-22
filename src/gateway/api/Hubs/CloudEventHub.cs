using CloudStreams.Gateway.Api.Client.Services;
using Microsoft.AspNetCore.SignalR;

namespace CloudStreams.Gateway.Api.Hubs;

/// <summary>
/// Represents the <see cref="Hub"/> used to observe <see cref="CloudEvent"/>s
/// </summary>
[Route("api/v1/ws/cloud-events")]
public class CloudEventHub
    : Hub<ICloudEventHubClient>, ICloudEventHub
{



}
