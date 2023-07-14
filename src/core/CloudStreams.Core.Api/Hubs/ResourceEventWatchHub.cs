﻿// Copyright © 2023-Present The Cloud Streams Authors
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

using CloudStreams.Core.Api.Client.Services;
using CloudStreams.Core.Api.Services;
using Hylo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CloudStreams.Core.Api.Hubs;

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
    public virtual Task Watch(ResourceDefinitionInfo definition, string? @namespace = null) => this.Controller.WatchResourcesAsync(this.Context.ConnectionId, definition, @namespace);

    /// <inheritdoc/>
    public virtual Task StopWatching(ResourceDefinitionInfo definition, string? @namespace = null) => this.Controller.StopWatchingResourcesAsync(this.Context.ConnectionId, definition, @namespace);

    /// <inheritdoc/>
    public override Task OnDisconnectedAsync(Exception? exception) => this.Controller.ReleaseConnectionResourcesAsync(this.Context.ConnectionId);


}
