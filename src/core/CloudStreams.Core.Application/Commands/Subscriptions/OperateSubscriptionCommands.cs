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

using Json.Patch;
using Json.Pointer;
using Neuroglia.Data.Infrastructure.EventSourcing;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Application.Commands.Subscriptions;

/// <summary>
/// Represents the command used to suspend a <see cref="Subscription"/>: the broker stops delivering, and keeps its offset, until it is resumed
/// </summary>
/// <param name="name">The name of the <see cref="Subscription"/> to suspend</param>
/// <remarks>
/// A dedicated command rather than a patch, so that users allowed to operate subscriptions can suspend them without being allowed to edit them (see #214)
/// </remarks>
public class SuspendSubscriptionCommand(string name)
    : Command<Subscription>
{

    /// <summary>
    /// Gets the name of the <see cref="Subscription"/> to suspend
    /// </summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentNullException(nameof(name)) : name;

}

/// <summary>
/// Represents the service used to handle <see cref="SuspendSubscriptionCommand"/>s
/// </summary>
/// <param name="resources">The service used to manage <see cref="IResource"/>s</param>
public class SuspendSubscriptionCommandHandler(IResourceRepository resources)
    : ICommandHandler<SuspendSubscriptionCommand, Subscription>
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<Subscription>> HandleAsync(SuspendSubscriptionCommand command, CancellationToken cancellationToken = default)
    {
        var subscription = await resources.GetAsync<Subscription>(command.Name, null, cancellationToken).ConfigureAwait(false);
        if (subscription == null) return this.NotFound();
        // suspending a subscription that is already suspended is not an error
        if (subscription.Spec.Suspended) return this.Ok(subscription);
        subscription = await resources.PatchAsync<Subscription>(new Patch(PatchType.JsonPatch, new JsonPatch(PatchOperation.Add(JsonPointer.Parse("/spec/suspended"), JsonValue.Create(true)))), command.Name, null, null, false, cancellationToken).ConfigureAwait(false);
        return this.Ok(subscription);
    }

}

/// <summary>
/// Represents the command used to resume a suspended or faulted <see cref="Subscription"/>. A suspended subscription is no longer suspended, and a faulted one has its fault
/// cleared, so that the cloud event that caused it is retried
/// </summary>
/// <param name="name">The name of the <see cref="Subscription"/> to resume</param>
/// <remarks>
/// A dedicated command rather than a patch, so that users allowed to operate subscriptions can resume them without being allowed to edit them or patch their status (see #147)
/// </remarks>
public class ResumeSubscriptionCommand(string name)
    : Command<Subscription>
{

    /// <summary>
    /// Gets the name of the <see cref="Subscription"/> to resume
    /// </summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentNullException(nameof(name)) : name;

}

/// <summary>
/// Represents the service used to handle <see cref="ResumeSubscriptionCommand"/>s
/// </summary>
/// <param name="resources">The service used to manage <see cref="IResource"/>s</param>
public class ResumeSubscriptionCommandHandler(IResourceRepository resources)
    : ICommandHandler<ResumeSubscriptionCommand, Subscription>
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<Subscription>> HandleAsync(ResumeSubscriptionCommand command, CancellationToken cancellationToken = default)
    {
        var subscription = await resources.GetAsync<Subscription>(command.Name, null, cancellationToken).ConfigureAwait(false);
        if (subscription == null) return this.NotFound();
        if (subscription.Spec.Suspended) subscription = await resources.PatchAsync<Subscription>(new Patch(PatchType.JsonPatch, new JsonPatch(PatchOperation.Add(JsonPointer.Parse("/spec/suspended"), JsonValue.Create(false)))), command.Name, null, null, false, cancellationToken).ConfigureAwait(false);
        // resuming a subscription that is neither suspended nor faulted, for example because it has been resumed concurrently, is not an error
        if (subscription.Status?.Stream?.Fault == null) return this.Ok(subscription);
        try
        {
            subscription = await resources.PatchStatusAsync<Subscription>(new Patch(PatchType.JsonPatch, new JsonPatch(PatchOperation.Remove(JsonPointer.Parse("/status/stream/fault")))), command.Name, null, null, false, cancellationToken).ConfigureAwait(false);
        }
        catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.Conflict)
        {
            // the fault has been cleared in the meantime
            subscription = await resources.GetAsync<Subscription>(command.Name, null, cancellationToken).ConfigureAwait(false);
            if (subscription == null) return this.NotFound();
        }
        return this.Ok(subscription);
    }

}

/// <summary>
/// Represents the command used to replay a <see cref="Subscription"/>, by delivering every cloud event matching it again, from the beginning of the stream
/// </summary>
/// <param name="name">The name of the <see cref="Subscription"/> to replay</param>
/// <remarks>
/// A dedicated command rather than a patch, so that users allowed to operate subscriptions can replay them without being allowed to change their spec (see #147)
/// </remarks>
public class ReplaySubscriptionCommand(string name)
    : Command<Subscription>
{

    /// <summary>
    /// Gets the name of the <see cref="Subscription"/> to replay
    /// </summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentNullException(nameof(name)) : name;

}

/// <summary>
/// Represents the service used to handle <see cref="ReplaySubscriptionCommand"/>s
/// </summary>
/// <param name="resources">The service used to manage <see cref="IResource"/>s</param>
public class ReplaySubscriptionCommandHandler(IResourceRepository resources)
    : ICommandHandler<ReplaySubscriptionCommand, Subscription>
{

    /// <inheritdoc/>
    /// <remarks>
    /// The broker only reacts to changes of the offset. A subscription that already starts at the beginning of the stream is therefore moved to its end first, then back to
    /// its beginning: should the second patch fail, the original offset is restored (see #196)
    /// </remarks>
    public virtual async Task<IOperationResult<Subscription>> HandleAsync(ReplaySubscriptionCommand command, CancellationToken cancellationToken = default)
    {
        var subscription = await resources.GetAsync<Subscription>(command.Name, null, cancellationToken).ConfigureAwait(false);
        if (subscription == null) return this.NotFound();
        var originalOffset = subscription.Spec.Stream?.Offset;
        if (originalOffset == StreamPosition.StartOfStream)
        {
            subscription = await this.SetOffsetAsync(subscription, StreamPosition.EndOfStream, cancellationToken).ConfigureAwait(false);
            try
            {
                subscription = await this.SetOffsetAsync(subscription, StreamPosition.StartOfStream, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await this.SetOffsetAsync(subscription, originalOffset.Value, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        else subscription = await this.SetOffsetAsync(subscription, StreamPosition.StartOfStream, cancellationToken).ConfigureAwait(false);
        return this.Ok(subscription);
    }

    /// <summary>
    /// Sets the offset of the specified <see cref="Subscription"/>
    /// </summary>
    /// <param name="subscription">The <see cref="Subscription"/> to set the offset of</param>
    /// <param name="offset">The offset to set</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The patched <see cref="Subscription"/></returns>
    protected virtual Task<Subscription> SetOffsetAsync(Subscription subscription, long offset, CancellationToken cancellationToken)
    {
        var operation = subscription.Spec.Stream == null
            ? PatchOperation.Add(JsonPointer.Parse("/spec/stream"), new JsonObject() { ["offset"] = offset })
            : PatchOperation.Add(JsonPointer.Parse("/spec/stream/offset"), JsonValue.Create(offset));
        return resources.PatchAsync<Subscription>(new Patch(PatchType.JsonPatch, new JsonPatch(operation)), subscription.GetName(), null, null, false, cancellationToken);
    }

}
