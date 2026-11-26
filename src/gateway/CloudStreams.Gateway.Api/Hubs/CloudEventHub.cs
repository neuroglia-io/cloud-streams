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

using CloudStreams.Core.Api;
using CloudStreams.Core.Api.Configuration;
using Microsoft.AspNetCore.SignalR;

namespace CloudStreams.Gateway.Api.Hubs;

/// <summary>
/// Represents the <see cref="Hub"/> used to observe <see cref="CloudEvent"/>s
/// </summary>
/// <param name="authenticationOptions">The options used to configure authentication, which determine the namespaces users may observe the cloud events of</param>
/// <remarks>
/// <para>
/// Each client belongs to a single group, which streams it either all cloud events or those of a source and/or a type, so that filtering happens on the gateway, and a
/// client never receives a cloud event twice: each cloud event is sent to the groups of its source and type (see #221).
/// </para>
/// <para>
/// Groups are bound to a namespace, or span every namespace, which only users granted access to the whole cluster may observe (see #215)
/// </para>
/// </remarks>
public class CloudEventHub(CloudStreamsAuthenticationOptions? authenticationOptions = null)
    : Hub<ICloudEventHubClient>, ICloudEventHub
{

    /// <summary>
    /// Gets the name of the group that streams all cloud events of every namespace, which clients allowed to read the whole cluster belong to until they subscribe
    /// </summary>
    public const string AllGroup = "*";

    const string GroupItemKey = "cloud-events.group";

    /// <summary>
    /// Gets the name of the group that streams the cloud events of the specified namespace, source and type
    /// </summary>
    /// <param name="namespace">The namespace of the cloud events to stream, if any. If not set, the cloud events of every namespace are streamed</param>
    /// <param name="source">The source of the cloud events to stream, if any</param>
    /// <param name="type">The type of the cloud events to stream, if any</param>
    /// <returns>The name of the group</returns>
    public static string GetGroupName(string? @namespace, string? source, string? type) => string.IsNullOrWhiteSpace(@namespace) && string.IsNullOrWhiteSpace(source) && string.IsNullOrWhiteSpace(type)
        ? AllGroup
        : $"namespace={(string.IsNullOrWhiteSpace(@namespace) ? "*" : @namespace)}\nsource={(string.IsNullOrWhiteSpace(source) ? "*" : source)}\ntype={(string.IsNullOrWhiteSpace(type) ? "*" : type)}";

    /// <summary>
    /// Gets the names of the groups the specified cloud event is streamed to
    /// </summary>
    /// <param name="namespace">The namespace the cloud event belongs to</param>
    /// <param name="e">The cloud event to get the groups of</param>
    /// <returns>The names of the groups the cloud event is streamed to</returns>
    public static IReadOnlyList<string> GetGroupNames(string @namespace, CloudEvent e) =>
    [
        AllGroup,
        GetGroupName(null, e.Source.OriginalString, null),
        GetGroupName(null, null, e.Type),
        GetGroupName(null, e.Source.OriginalString, e.Type),
        GetGroupName(@namespace, null, null),
        GetGroupName(@namespace, e.Source.OriginalString, null),
        GetGroupName(@namespace, null, e.Type),
        GetGroupName(@namespace, e.Source.OriginalString, e.Type)
    ];

    /// <inheritdoc/>
    /// <remarks>Clients allowed to read the whole cluster stream every cloud event until they subscribe. Other clients stream none until they subscribe to a namespace</remarks>
    public override async Task OnConnectedAsync()
    {
        if (this.HasAccess(null))
        {
            await this.Groups.AddToGroupAsync(this.Context.ConnectionId, AllGroup).ConfigureAwait(false);
            this.Context.Items[GroupItemKey] = AllGroup;
        }
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual async Task Subscribe(string? source, string? type, string? @namespace = null)
    {
        if (string.IsNullOrWhiteSpace(@namespace)) @namespace = null;
        if (!this.HasAccess(@namespace)) throw new HubException(@namespace == null ? "Observing the cloud events of every namespace requires access to the whole cluster: subscribe to a namespace" : $"Access to the namespace '{@namespace}' is denied");
        var group = GetGroupName(@namespace, source, type);
        if (this.Context.Items.TryGetValue(GroupItemKey, out var current) && current is string previous)
        {
            if (previous == group) return;
            await this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, previous).ConfigureAwait(false);
        }
        await this.Groups.AddToGroupAsync(this.Context.ConnectionId, group).ConfigureAwait(false);
        this.Context.Items[GroupItemKey] = group;
    }

    /// <summary>
    /// Determines whether or not the calling user may read the cloud events of the specified namespace
    /// </summary>
    /// <param name="namespace">The namespace to check, or null to check the whole cluster</param>
    /// <returns>A boolean indicating whether or not the calling user may read the cloud events of the namespace</returns>
    protected virtual bool HasAccess(string? @namespace) => authenticationOptions == null || this.Context.User == null || authenticationOptions.Dashboard.GetAccessLevel(this.Context.User, @namespace) >= CloudStreamsAccessLevel.Read;

}
