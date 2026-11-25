// Copyright © 2024-Present The Cloud Streams Authors
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

using Microsoft.AspNetCore.SignalR;

namespace CloudStreams.Gateway.Api.Hubs;

/// <summary>
/// Represents the <see cref="Hub"/> used to observe <see cref="CloudEvent"/>s
/// </summary>
/// <remarks>
/// Each client belongs to a single group, which streams it either all cloud events or those of a source and/or a type, so that filtering happens on the gateway, and a
/// client never receives a cloud event twice: each cloud event is sent to the groups of its source and type (see #221)
/// </remarks>
public class CloudEventHub
    : Hub<ICloudEventHubClient>, ICloudEventHub
{

    /// <summary>
    /// Gets the name of the group that streams all cloud events, which clients belong to until they subscribe
    /// </summary>
    public const string AllGroup = "*";

    const string GroupItemKey = "cloud-events.group";

    /// <summary>
    /// Gets the name of the group that streams the cloud events of the specified source and type
    /// </summary>
    /// <param name="source">The source of the cloud events the group streams, if any</param>
    /// <param name="type">The type of the cloud events the group streams, if any</param>
    /// <returns>The name of the group</returns>
    public static string GetGroupName(string? source, string? type) => string.IsNullOrWhiteSpace(source) && string.IsNullOrWhiteSpace(type)
        ? AllGroup
        : $"source={(string.IsNullOrWhiteSpace(source) ? "*" : source)}\ntype={(string.IsNullOrWhiteSpace(type) ? "*" : type)}";

    /// <summary>
    /// Gets the names of the groups that stream the specified cloud event
    /// </summary>
    /// <param name="e">The cloud event to get the groups of</param>
    /// <returns>The names of the groups that stream the cloud event</returns>
    public static IReadOnlyList<string> GetGroupNames(CloudEvent e) =>
    [
        AllGroup,
        GetGroupName(e.Source.OriginalString, null),
        GetGroupName(null, e.Type),
        GetGroupName(e.Source.OriginalString, e.Type)
    ];

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        await this.Groups.AddToGroupAsync(this.Context.ConnectionId, AllGroup).ConfigureAwait(false);
        this.Context.Items[GroupItemKey] = AllGroup;
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual async Task Subscribe(string? source, string? type)
    {
        var group = GetGroupName(source, type);
        if (this.Context.Items.TryGetValue(GroupItemKey, out var current) && current is string previous)
        {
            if (previous == group) return;
            await this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, previous).ConfigureAwait(false);
        }
        await this.Groups.AddToGroupAsync(this.Context.ConnectionId, group).ConfigureAwait(false);
        this.Context.Items[GroupItemKey] = group;
    }

}
