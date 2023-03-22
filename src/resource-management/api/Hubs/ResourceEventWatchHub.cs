// Copyright © 2023-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CloudStreams.Core;
using CloudStreams.ResourceManagement.Api.Client.Services;
using CloudStreams.ResourceManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CloudStreams.ResourceManagement.Api.Hubs;

/// <summary>
/// Represents the <see cref="Hub"/> used to notify clients about resource-related changes
/// </summary>
[Route("api/resource-management/v1/ws/watch")]
public class ResourceEventWatchHub
    : Hub<IResourceEventWatchHubClient>, IResourceEventWatchHub
{

    /// <summary>
    /// Initializes a new <see cref="ResourceEventWatchHub"/>
    /// </summary>
    /// <param name="controller">The service used to control <see cref="ResourceEventWatchHub"/>s</param>
    public ResourceEventWatchHub(ResourceWatchEventHubController controller)
    {
        this.Controller = controller;
    }

    /// <summary>
    /// Gets the service used to control <see cref="ResourceEventWatchHub"/>s
    /// </summary>
    protected ResourceWatchEventHubController Controller { get; }

    /// <inheritdoc/>
    public virtual Task Watch(ResourceType type, string? @namespace = null) => this.Controller.WatchResourcesAsync(this.Context.ConnectionId, type, @namespace);

    /// <inheritdoc/>
    public virtual Task StopWatching(ResourceType type, string? @namespace = null) => this.Controller.StopWatchingResourcesAsync(this.Context.ConnectionId, type, @namespace);

    /// <inheritdoc/>
    public override Task OnDisconnectedAsync(Exception? exception) => this.Controller.ReleaseConnectionResourcesAsync(this.Context.ConnectionId);


}
