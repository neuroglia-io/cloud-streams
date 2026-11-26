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

using CloudStreams.Core.Application.Commands.Subscriptions;
using CloudStreams.Core.Application.Queries.Resources.Generic;
using CloudStreams.Core.Application.Queries.Subscriptions;
using CloudStreams.Core.Api.Configuration;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Neuroglia.Data.Infrastructure.EventSourcing;
using System.ComponentModel;

namespace CloudStreams.Core.Api.Tools;

/// <summary>
/// Represents the MCP toolset used to inspect and operate subscriptions (see #217)
/// </summary>
/// <param name="mediator">The service used to mediate calls</param>
/// <param name="httpContextAccessor">The service used to access the current HTTP context, and the user calling the tools</param>
/// <remarks>
/// The tools that inspect subscriptions require the reader role, and the tools that operate them the operator role, as the endpoints they mirror do, in the
/// namespace of the subscription (see #215)
/// </remarks>
[McpServerToolType]
public class SubscriptionToolset(IMediator mediator, IHttpContextAccessor httpContextAccessor)
{

    /// <summary>
    /// Lists the subscriptions, with their phase, lag, subscriber state and fault, if any
    /// </summary>
    /// <param name="namespace">The namespace to list the subscriptions of, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The health of each subscription</returns>
    [McpServerTool(Name = "ListSubscriptions"), Description("Lists the subscriptions, with their phase ('failed' if faulted), the amount of events they are behind ('lag'), the state of their subscriber and the reason of their fault, if any")]
    public virtual async Task<IEnumerable<SubscriptionHealth>> ListSubscriptionsAsync([Description("The namespace to list the subscriptions of, if any. Lists the subscriptions of every namespace the user may read if not set")] string? @namespace = null, CancellationToken cancellationToken = default)
    {
        ToolAuthorization.EnsureAccess(httpContextAccessor, @namespace, CloudStreamsAccessLevel.Read);
        var subscriptions = Unwrap(await mediator.ExecuteAsync(new GetSubscriptionsHealthQuery(@namespace, null), cancellationToken).ConfigureAwait(false));
        var namespaces = httpContextAccessor.HttpContext?.GetAccessibleNamespaces(CloudStreamsAccessLevel.Read);
        return namespaces == null ? subscriptions : subscriptions.Where(s => s.Namespace != null && namespaces.Contains(s.Namespace)).ToList();
    }

    /// <summary>
    /// Gets the specified subscription
    /// </summary>
    /// <param name="namespace">The namespace the subscription belongs to. Defaults to the default namespace</param>
    /// <param name="name">The name of the subscription to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The specified subscription</returns>
    [McpServerTool(Name = "GetSubscription"), Description("Gets the specified subscription, with its spec and status, which describes its acknowledged offset, its fault and the state of its subscriber")]
    public virtual async Task<Subscription> GetSubscriptionAsync([Description("The name of the subscription to get")] string name, [Description("The namespace the subscription belongs to. Defaults to 'default'")] string? @namespace = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ToolAuthorization.EnsureAccess(httpContextAccessor, @namespace ?? Namespace.DefaultNamespaceName, CloudStreamsAccessLevel.Read);
        return Unwrap(await mediator.ExecuteAsync(new GetResourceQuery<Subscription>(name, @namespace ?? Namespace.DefaultNamespaceName), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Lists the cloud events the specified subscription failed to deliver, most recent first
    /// </summary>
    /// <param name="namespace">The namespace the subscription belongs to. Defaults to the default namespace</param>
    /// <param name="name">The name of the subscription</param>
    /// <param name="length">The maximum amount of dead letters to list</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The dead letters of the subscription</returns>
    [McpServerTool(Name = "ListDeadLetters"), Description("Lists the cloud events the specified subscription failed to deliver and stored in its dead-letter stream, most recent first, with the reason why")]
    public virtual async Task<IEnumerable<DeadLetter>> ListDeadLettersAsync([Description("The name of the subscription")] string name, [Description("The maximum amount of dead letters to list, up to 100")] ulong length = 20, [Description("The namespace the subscription belongs to. Defaults to 'default'")] string? @namespace = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ToolAuthorization.EnsureAccess(httpContextAccessor, @namespace ?? Namespace.DefaultNamespaceName, CloudStreamsAccessLevel.Read);
        var deadLetters = Unwrap(await mediator.ExecuteAsync(new ReadSubscriptionDeadLettersQuery(name, StreamReadDirection.Backwards, null, length, @namespace), cancellationToken).ConfigureAwait(false));
        return await deadLetters.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Suspends the specified subscription
    /// </summary>
    /// <param name="namespace">The namespace the subscription belongs to. Defaults to the default namespace</param>
    /// <param name="name">The name of the subscription to suspend</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The suspended subscription</returns>
    [McpServerTool(Name = "SuspendSubscription"), Description("Suspends the specified subscription: nothing is delivered until it is resumed, and delivery then resumes where it stopped. Requires the operator role")]
    public virtual async Task<Subscription> SuspendSubscriptionAsync([Description("The name of the subscription to suspend")] string name, [Description("The namespace the subscription belongs to. Defaults to 'default'")] string? @namespace = null, CancellationToken cancellationToken = default)
    {
        ToolAuthorization.EnsureAccess(httpContextAccessor, @namespace ?? Namespace.DefaultNamespaceName, CloudStreamsAccessLevel.Operate);
        return Unwrap(await mediator.ExecuteAsync(new SuspendSubscriptionCommand(name, @namespace), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Resumes the specified suspended or faulted subscription
    /// </summary>
    /// <param name="namespace">The namespace the subscription belongs to. Defaults to the default namespace</param>
    /// <param name="name">The name of the subscription to resume</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The resumed subscription</returns>
    [McpServerTool(Name = "ResumeSubscription"), Description("Resumes the specified suspended or faulted subscription. The fault of a faulted subscription is cleared, and the event that caused it is retried. Requires the operator role")]
    public virtual async Task<Subscription> ResumeSubscriptionAsync([Description("The name of the subscription to resume")] string name, [Description("The namespace the subscription belongs to. Defaults to 'default'")] string? @namespace = null, CancellationToken cancellationToken = default)
    {
        ToolAuthorization.EnsureAccess(httpContextAccessor, @namespace ?? Namespace.DefaultNamespaceName, CloudStreamsAccessLevel.Operate);
        return Unwrap(await mediator.ExecuteAsync(new ResumeSubscriptionCommand(name, @namespace), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Replays the specified subscription, from the beginning of the stream or from the specified time
    /// </summary>
    /// <param name="namespace">The namespace the subscription belongs to. Defaults to the default namespace</param>
    /// <param name="name">The name of the subscription to replay</param>
    /// <param name="from">The time from which to replay the subscription, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The replayed subscription</returns>
    [McpServerTool(Name = "ReplaySubscription"), Description("Replays the specified subscription: every matching event is delivered again, from the beginning of the stream, or from the first event recorded at or after the specified time. Requires the operator role")]
    public virtual async Task<Subscription> ReplaySubscriptionAsync([Description("The name of the subscription to replay")] string name, [Description("The time, in ISO 8601 format, from which to replay the subscription, if any")] DateTimeOffset? from = null, [Description("The namespace the subscription belongs to. Defaults to 'default'")] string? @namespace = null, CancellationToken cancellationToken = default)
    {
        ToolAuthorization.EnsureAccess(httpContextAccessor, @namespace ?? Namespace.DefaultNamespaceName, CloudStreamsAccessLevel.Operate);
        return Unwrap(await mediator.ExecuteAsync(new ReplaySubscriptionCommand(name, from, @namespace), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Skips the cloud event that faulted the specified subscription
    /// </summary>
    /// <param name="namespace">The namespace the subscription belongs to. Defaults to the default namespace</param>
    /// <param name="name">The name of the faulted subscription</param>
    /// <param name="offset">The offset of the cloud event to skip, which must be the subscription's faulted offset</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The subscription, moved past the skipped cloud event</returns>
    [McpServerTool(Name = "SkipSubscriptionEvent"), Description("Skips the event that faulted the specified subscription, which is never delivered: delivery resumes with the next event. The offset must be the subscription's 'status.stream.faultedOffset'. Requires the operator role")]
    public virtual async Task<Subscription> SkipSubscriptionEventAsync([Description("The name of the faulted subscription")] string name, [Description("The offset of the event to skip, as found in the subscription's 'status.stream.faultedOffset'")] ulong offset, [Description("The namespace the subscription belongs to. Defaults to 'default'")] string? @namespace = null, CancellationToken cancellationToken = default)
    {
        ToolAuthorization.EnsureAccess(httpContextAccessor, @namespace ?? Namespace.DefaultNamespaceName, CloudStreamsAccessLevel.Operate);
        return Unwrap(await mediator.ExecuteAsync(new SkipSubscriptionEventCommand(name, offset, @namespace), cancellationToken).ConfigureAwait(false));
    }

    static T Unwrap<T>(IOperationResult<T> result) => ToolAuthorization.Unwrap(result);

}

/// <summary>
/// Exposes helpers shared by MCP toolsets
/// </summary>
public static class ToolAuthorization
{

    /// <summary>
    /// Ensures that the user calling a tool is granted the specified level of access in the specified namespace
    /// </summary>
    /// <param name="httpContextAccessor">The service used to access the current HTTP context</param>
    /// <param name="namespace">The namespace to access, or null to access the whole cluster</param>
    /// <param name="level">The level of access to require</param>
    /// <exception cref="UnauthorizedAccessException">The calling user is not granted the specified level of access</exception>
    public static void EnsureAccess(IHttpContextAccessor httpContextAccessor, string? @namespace, CloudStreamsAccessLevel level)
    {
        var httpContext = httpContextAccessor.HttpContext ?? throw new UnauthorizedAccessException("The tool can only be called over HTTP");
        if (httpContext.HasAccess(@namespace, level)) return;
        throw new UnauthorizedAccessException(string.IsNullOrWhiteSpace(@namespace) ? $"The tool requires the '{level}' access to the whole cluster" : $"The tool requires the '{level}' access to the namespace '{@namespace}'");
    }

    /// <summary>
    /// Gets the data of the specified result, or throws an exception that describes why it failed
    /// </summary>
    /// <typeparam name="T">The type of the result's data</typeparam>
    /// <param name="result">The result to unwrap</param>
    /// <returns>The data of the result</returns>
    public static T Unwrap<T>(IOperationResult<T> result)
    {
        if (result.IsSuccess() && result.Data != null) return result.Data;
        var errors = result.Errors?.Any() == true ? string.Join(Environment.NewLine, result.Errors.Select(e => $"[{e.Status}] {e.Title}: {e.Detail}")) : $"The operation failed with status '{result.Status}'";
        throw new InvalidOperationException(errors);
    }

}
