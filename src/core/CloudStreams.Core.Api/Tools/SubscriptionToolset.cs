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
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Neuroglia.Data.Infrastructure.EventSourcing;
using System.ComponentModel;

namespace CloudStreams.Core.Api.Tools;

/// <summary>
/// Represents the MCP toolset used to inspect and operate subscriptions (see #217)
/// </summary>
/// <param name="mediator">The service used to mediate calls</param>
/// <param name="authorization">The service used to authorize the tools that operate subscriptions</param>
/// <param name="httpContextAccessor">The service used to access the current HTTP context, and the user calling the tools</param>
/// <remarks>
/// The MCP endpoint requires the reader role, which is enough for the tools that inspect subscriptions. The tools that operate them require the operator role, as the
/// endpoints they mirror do
/// </remarks>
[McpServerToolType]
public class SubscriptionToolset(IMediator mediator, IAuthorizationService authorization, IHttpContextAccessor httpContextAccessor)
{

    /// <summary>
    /// Lists the subscriptions, with their phase, lag, subscriber state and fault, if any
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The health of each subscription</returns>
    [McpServerTool(Name = "ListSubscriptions"), Description("Lists the subscriptions, with their phase ('failed' if faulted), the amount of events they are behind ('lag'), the state of their subscriber and the reason of their fault, if any")]
    public virtual async Task<IEnumerable<SubscriptionHealth>> ListSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        return Unwrap(await mediator.ExecuteAsync(new GetSubscriptionsHealthQuery(null, null), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Gets the specified subscription
    /// </summary>
    /// <param name="name">The name of the subscription to get</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The specified subscription</returns>
    [McpServerTool(Name = "GetSubscription"), Description("Gets the specified subscription, with its spec and status, which describes its acknowledged offset, its fault and the state of its subscriber")]
    public virtual async Task<Subscription> GetSubscriptionAsync([Description("The name of the subscription to get")] string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Unwrap(await mediator.ExecuteAsync(new GetResourceQuery<Subscription>(name, null), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Lists the cloud events the specified subscription failed to deliver, most recent first
    /// </summary>
    /// <param name="name">The name of the subscription</param>
    /// <param name="length">The maximum amount of dead letters to list</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The dead letters of the subscription</returns>
    [McpServerTool(Name = "ListDeadLetters"), Description("Lists the cloud events the specified subscription failed to deliver and stored in its dead-letter stream, most recent first, with the reason why")]
    public virtual async Task<IEnumerable<DeadLetter>> ListDeadLettersAsync([Description("The name of the subscription")] string name, [Description("The maximum amount of dead letters to list, up to 100")] ulong length = 20, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var deadLetters = Unwrap(await mediator.ExecuteAsync(new ReadSubscriptionDeadLettersQuery(name, StreamReadDirection.Backwards, null, length), cancellationToken).ConfigureAwait(false));
        return await deadLetters.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Suspends the specified subscription
    /// </summary>
    /// <param name="name">The name of the subscription to suspend</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The suspended subscription</returns>
    [McpServerTool(Name = "SuspendSubscription"), Description("Suspends the specified subscription: nothing is delivered until it is resumed, and delivery then resumes where it stopped. Requires the operator role")]
    public virtual async Task<Subscription> SuspendSubscriptionAsync([Description("The name of the subscription to suspend")] string name, CancellationToken cancellationToken = default)
    {
        await this.AuthorizeAsync(CloudStreamsAuthenticationDefaults.OperatePolicy).ConfigureAwait(false);
        return Unwrap(await mediator.ExecuteAsync(new SuspendSubscriptionCommand(name), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Resumes the specified suspended or faulted subscription
    /// </summary>
    /// <param name="name">The name of the subscription to resume</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The resumed subscription</returns>
    [McpServerTool(Name = "ResumeSubscription"), Description("Resumes the specified suspended or faulted subscription. The fault of a faulted subscription is cleared, and the event that caused it is retried. Requires the operator role")]
    public virtual async Task<Subscription> ResumeSubscriptionAsync([Description("The name of the subscription to resume")] string name, CancellationToken cancellationToken = default)
    {
        await this.AuthorizeAsync(CloudStreamsAuthenticationDefaults.OperatePolicy).ConfigureAwait(false);
        return Unwrap(await mediator.ExecuteAsync(new ResumeSubscriptionCommand(name), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Replays the specified subscription, from the beginning of the stream or from the specified time
    /// </summary>
    /// <param name="name">The name of the subscription to replay</param>
    /// <param name="from">The time from which to replay the subscription, if any</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The replayed subscription</returns>
    [McpServerTool(Name = "ReplaySubscription"), Description("Replays the specified subscription: every matching event is delivered again, from the beginning of the stream, or from the first event recorded at or after the specified time. Requires the operator role")]
    public virtual async Task<Subscription> ReplaySubscriptionAsync([Description("The name of the subscription to replay")] string name, [Description("The time, in ISO 8601 format, from which to replay the subscription, if any")] DateTimeOffset? from = null, CancellationToken cancellationToken = default)
    {
        await this.AuthorizeAsync(CloudStreamsAuthenticationDefaults.OperatePolicy).ConfigureAwait(false);
        return Unwrap(await mediator.ExecuteAsync(new ReplaySubscriptionCommand(name, from), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Ensures that the calling user satisfies the specified policy
    /// </summary>
    /// <param name="policy">The name of the policy to satisfy</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <exception cref="UnauthorizedAccessException">The calling user does not satisfy the policy</exception>
    protected virtual Task AuthorizeAsync(string policy) => ToolAuthorization.AuthorizeAsync(authorization, httpContextAccessor, policy);

    static T Unwrap<T>(IOperationResult<T> result) => ToolAuthorization.Unwrap(result);

}

/// <summary>
/// Exposes helpers shared by MCP toolsets
/// </summary>
public static class ToolAuthorization
{

    /// <summary>
    /// Ensures that the user calling a tool satisfies the specified policy
    /// </summary>
    /// <param name="authorization">The service used to authorize the user</param>
    /// <param name="httpContextAccessor">The service used to access the current HTTP context</param>
    /// <param name="policy">The name of the policy to satisfy</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <exception cref="UnauthorizedAccessException">The calling user does not satisfy the policy</exception>
    public static async Task AuthorizeAsync(IAuthorizationService authorization, IHttpContextAccessor httpContextAccessor, string policy)
    {
        var user = httpContextAccessor.HttpContext?.User ?? throw new UnauthorizedAccessException("The tool can only be called over HTTP");
        var result = await authorization.AuthorizeAsync(user, policy).ConfigureAwait(false);
        if (!result.Succeeded) throw new UnauthorizedAccessException($"The tool requires a role that satisfies the '{policy}' policy");
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
