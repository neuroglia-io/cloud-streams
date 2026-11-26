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
using CloudStreams.Core.Application.Services;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Application.Commands.Subscriptions;

/// <summary>
/// Represents the command used to suspend a <see cref="Subscription"/>: the broker stops delivering, and keeps its offset, until it is resumed
/// </summary>
/// <param name="name">The name of the <see cref="Subscription"/> to suspend</param>
/// <param name="namespace">The namespace the resource belongs to, if any</param>
/// <remarks>
/// A dedicated command rather than a patch, so that users allowed to operate subscriptions can suspend them without being allowed to edit them (see #214)
/// </remarks>
public class SuspendSubscriptionCommand(string name, string? @namespace = null)
    : Command<Subscription>
{

    /// <summary>
    /// Gets the namespace the resource belongs to
    /// </summary>
    public virtual string Namespace { get; } = string.IsNullOrWhiteSpace(@namespace) ? Neuroglia.Data.Infrastructure.ResourceOriented.Namespace.DefaultNamespaceName : @namespace;

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
        var subscription = await resources.GetAsync<Subscription>(command.Name, command.Namespace, cancellationToken).ConfigureAwait(false);
        if (subscription == null) return this.NotFound();
        // suspending a subscription that is already suspended is not an error
        if (subscription.Spec.Suspended) return this.Ok(subscription);
        subscription = await resources.PatchAsync<Subscription>(new Patch(PatchType.JsonPatch, new JsonPatch(PatchOperation.Add(JsonPointer.Parse("/spec/suspended"), JsonValue.Create(true)))), command.Name, command.Namespace, null, false, cancellationToken).ConfigureAwait(false);
        return this.Ok(subscription);
    }

}

/// <summary>
/// Represents the command used to resume a suspended or faulted <see cref="Subscription"/>. A suspended subscription is no longer suspended, and a faulted one has its fault
/// cleared, so that the cloud event that caused it is retried
/// </summary>
/// <param name="name">The name of the <see cref="Subscription"/> to resume</param>
/// <param name="namespace">The namespace the resource belongs to, if any</param>
/// <remarks>
/// A dedicated command rather than a patch, so that users allowed to operate subscriptions can resume them without being allowed to edit them or patch their status (see #147)
/// </remarks>
public class ResumeSubscriptionCommand(string name, string? @namespace = null)
    : Command<Subscription>
{

    /// <summary>
    /// Gets the namespace the resource belongs to
    /// </summary>
    public virtual string Namespace { get; } = string.IsNullOrWhiteSpace(@namespace) ? Neuroglia.Data.Infrastructure.ResourceOriented.Namespace.DefaultNamespaceName : @namespace;

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
        var subscription = await resources.GetAsync<Subscription>(command.Name, command.Namespace, cancellationToken).ConfigureAwait(false);
        if (subscription == null) return this.NotFound();
        if (subscription.Spec.Suspended) subscription = await resources.PatchAsync<Subscription>(new Patch(PatchType.JsonPatch, new JsonPatch(PatchOperation.Add(JsonPointer.Parse("/spec/suspended"), JsonValue.Create(false)))), command.Name, command.Namespace, null, false, cancellationToken).ConfigureAwait(false);
        // resuming a subscription that is neither suspended nor faulted, for example because it has been resumed concurrently, is not an error
        if (subscription.Status?.Stream?.Fault == null) return this.Ok(subscription);
        try
        {
            // the offset of the cloud event that caused the fault goes with it (see #224)
            List<PatchOperation> operations = [PatchOperation.Remove(JsonPointer.Parse("/status/stream/fault"))];
            if (subscription.Status.Stream.FaultedOffset.HasValue) operations.Add(PatchOperation.Remove(JsonPointer.Parse("/status/stream/faultedOffset")));
            subscription = await resources.PatchStatusAsync<Subscription>(new Patch(PatchType.JsonPatch, new JsonPatch(operations)), command.Name, command.Namespace, null, false, cancellationToken).ConfigureAwait(false);
        }
        catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.Conflict)
        {
            // the fault has been cleared in the meantime
            subscription = await resources.GetAsync<Subscription>(command.Name, command.Namespace, cancellationToken).ConfigureAwait(false);
            if (subscription == null) return this.NotFound();
        }
        return this.Ok(subscription);
    }

}


/// <summary>
/// Represents the command used to replay a <see cref="Subscription"/>, by delivering every cloud event matching it again, from the beginning of the stream or from the
/// specified time
/// </summary>
/// <param name="name">The name of the <see cref="Subscription"/> to replay</param>
/// <param name="namespace">The namespace the resource belongs to, if any</param>
/// <param name="from">The time from which to replay the <see cref="Subscription"/>, if any. If not set, the <see cref="Subscription"/> is replayed from the beginning of the stream</param>
/// <remarks>
/// A dedicated command rather than a patch, so that users allowed to operate subscriptions can replay them without being allowed to change their spec (see #147)
/// </remarks>
public class ReplaySubscriptionCommand(string name, DateTimeOffset? from = null, string? @namespace = null)
    : Command<Subscription>
{

    /// <summary>
    /// Gets the namespace the resource belongs to
    /// </summary>
    public virtual string Namespace { get; } = string.IsNullOrWhiteSpace(@namespace) ? Neuroglia.Data.Infrastructure.ResourceOriented.Namespace.DefaultNamespaceName : @namespace;

    /// <summary>
    /// Gets the name of the <see cref="Subscription"/> to replay
    /// </summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentNullException(nameof(name)) : name;

    /// <summary>
    /// Gets the time from which to replay the <see cref="Subscription"/>, if any
    /// </summary>
    public DateTimeOffset? From { get; } = from;

}

/// <summary>
/// Represents the service used to handle <see cref="ReplaySubscriptionCommand"/>s
/// </summary>
/// <param name="resources">The service used to manage <see cref="IResource"/>s</param>
/// <param name="eventStore">The service used to find the offset of the first cloud event recorded at or after the time to replay from</param>
public class ReplaySubscriptionCommandHandler(IResourceRepository resources, ICloudEventStore eventStore)
    : ICommandHandler<ReplaySubscriptionCommand, Subscription>
{

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// A time is resolved to the offset of the first cloud event recorded at or after it, in the stream or partition the subscription consumes. If none has been, the
    /// subscription is replayed from the current length of the stream, which is to say from the next cloud event (see #211).
    /// </para>
    /// <para>
    /// The broker only reacts to changes of the offset. A subscription whose offset already is the one to replay from is therefore moved to another offset first, then
    /// back: should the second patch fail, the original offset is restored (see #196)
    /// </para>
    /// </remarks>
    public virtual async Task<IOperationResult<Subscription>> HandleAsync(ReplaySubscriptionCommand command, CancellationToken cancellationToken = default)
    {
        var subscription = await resources.GetAsync<Subscription>(command.Name, command.Namespace, cancellationToken).ConfigureAwait(false);
        if (subscription == null) return this.NotFound();
        var offset = command.From.HasValue ? await this.ResolveOffsetAsync(subscription, command.From.Value, cancellationToken).ConfigureAwait(false) : StreamPosition.StartOfStream;
        return this.Ok(await SubscriptionOffsets.MoveAsync(resources, subscription, offset, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Resolves the offset of the first cloud event recorded at or after the specified time, in the stream or partition the specified subscription consumes
    /// </summary>
    /// <param name="subscription">The subscription to resolve the offset for</param>
    /// <param name="time">The time to resolve</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The resolved offset</returns>
    protected virtual async Task<long> ResolveOffsetAsync(Subscription subscription, DateTimeOffset time, CancellationToken cancellationToken)
    {
        var partition = subscription.Spec.Partition;
        var found = await eventStore.FindOffsetAsync(subscription.GetNamespace(), time, partition, cancellationToken).ConfigureAwait(false);
        if (found.HasValue) return (long)found.Value;
        try
        {
            return (long)(partition == null
                ? (await eventStore.GetStreamMetadataAsync(subscription.GetNamespace(), cancellationToken).ConfigureAwait(false)).Length ?? 0
                : (await eventStore.GetPartitionMetadataAsync(subscription.GetNamespace(), partition, cancellationToken).ConfigureAwait(false)).Length);
        }
        catch (Exception ex) when (ex is StreamNotFoundException || (ex is ProblemDetailsException notFound && notFound.Problem.Status == (int)HttpStatusCode.NotFound))
        {
            // nothing has been recorded yet: the subscription receives every cloud event
            return StreamPosition.StartOfStream;
        }
    }

}

/// <summary>
/// Represents the command used to skip the cloud event that faulted a <see cref="Subscription"/>: delivery resumes with the cloud event that follows it (see #224)
/// </summary>
/// <param name="name">The name of the <see cref="Subscription"/> to skip the faulted cloud event of</param>
/// <param name="namespace">The namespace the resource belongs to, if any</param>
/// <param name="offset">The offset of the cloud event to skip, which must be the one that faulted the subscription, so that a subscription faulted again in the meantime by
/// another cloud event does not skip that one unseen</param>
/// <remarks>Like replaying, a dedicated command rather than a patch, so that users allowed to operate subscriptions can skip a cloud event without being allowed to change their spec</remarks>
public class SkipSubscriptionEventCommand(string name, ulong offset, string? @namespace = null)
    : Command<Subscription>
{

    /// <summary>
    /// Gets the namespace the resource belongs to
    /// </summary>
    public virtual string Namespace { get; } = string.IsNullOrWhiteSpace(@namespace) ? Neuroglia.Data.Infrastructure.ResourceOriented.Namespace.DefaultNamespaceName : @namespace;

    /// <summary>
    /// Gets the name of the <see cref="Subscription"/> to skip the faulted cloud event of
    /// </summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentNullException(nameof(name)) : name;

    /// <summary>
    /// Gets the offset of the cloud event to skip
    /// </summary>
    public ulong Offset { get; } = offset;

}

/// <summary>
/// Represents the service used to handle <see cref="SkipSubscriptionEventCommand"/>s
/// </summary>
/// <param name="resources">The service used to manage <see cref="IResource"/>s</param>
public class SkipSubscriptionEventCommandHandler(IResourceRepository resources)
    : ICommandHandler<SkipSubscriptionEventCommand, Subscription>
{

    /// <summary>
    /// Gets the type of the problem returned when the subscription is not faulted by the cloud event to skip
    /// </summary>
    public static readonly Uri SkipConflictProblemType = new("https://cloud-streams.io/problems/skip-conflict");

    /// <inheritdoc/>
    /// <remarks>The offset is moved past the faulted cloud event, which the broker answers by clearing the fault and resuming delivery from there</remarks>
    public virtual async Task<IOperationResult<Subscription>> HandleAsync(SkipSubscriptionEventCommand command, CancellationToken cancellationToken = default)
    {
        var subscription = await resources.GetAsync<Subscription>(command.Name, command.Namespace, cancellationToken).ConfigureAwait(false);
        if (subscription == null) return this.NotFound();
        var stream = subscription.Status?.Stream;
        if (stream?.Fault == null) return Conflict($"The subscription '{command.Name}' is not faulted: there is no cloud event to skip");
        if (!stream.FaultedOffset.HasValue) return Conflict($"The offset of the cloud event that faulted the subscription '{command.Name}' is unknown. Set its 'spec.stream.offset' to the offset that follows it to skip it");
        if (stream.FaultedOffset.Value != command.Offset) return Conflict($"The subscription '{command.Name}' has been faulted by the cloud event at offset '{stream.FaultedOffset.Value}', not '{command.Offset}'");
        return this.Ok(await SubscriptionOffsets.MoveAsync(resources, subscription, (long)command.Offset + 1, cancellationToken).ConfigureAwait(false));
    }

    OperationResult<Subscription> Conflict(string detail) => new((int)HttpStatusCode.Conflict, errors: [new Error(SkipConflictProblemType, "Conflict", (int)HttpStatusCode.Conflict, detail)]);

}

/// <summary>
/// Provides helpers to move the offset of <see cref="Subscription"/>s
/// </summary>
static class SubscriptionOffsets
{

    /// <summary>
    /// Moves the specified <see cref="Subscription"/> to the specified offset, which the broker delivers from next
    /// </summary>
    /// <param name="resources">The service used to manage <see cref="IResource"/>s</param>
    /// <param name="subscription">The <see cref="Subscription"/> to move</param>
    /// <param name="offset">The offset to move the <see cref="Subscription"/> to</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The moved <see cref="Subscription"/></returns>
    /// <remarks>
    /// The broker only reacts to changes of the offset. A subscription whose offset already is the one to move to is therefore moved to another offset first, then
    /// back: should the second patch fail, the original offset is restored (see #196)
    /// </remarks>
    public static async Task<Subscription> MoveAsync(IResourceRepository resources, Subscription subscription, long offset, CancellationToken cancellationToken)
    {
        var originalOffset = subscription.Spec.Stream?.Offset;
        if (originalOffset != offset) return await SetAsync(resources, subscription, offset, cancellationToken).ConfigureAwait(false);
        subscription = await SetAsync(resources, subscription, offset == StreamPosition.EndOfStream ? StreamPosition.StartOfStream : StreamPosition.EndOfStream, cancellationToken).ConfigureAwait(false);
        try
        {
            return await SetAsync(resources, subscription, offset, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await SetAsync(resources, subscription, originalOffset.Value, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    static Task<Subscription> SetAsync(IResourceRepository resources, Subscription subscription, long offset, CancellationToken cancellationToken)
    {
        var operation = subscription.Spec.Stream == null
            ? PatchOperation.Add(JsonPointer.Parse("/spec/stream"), new JsonObject() { ["offset"] = offset })
            : PatchOperation.Add(JsonPointer.Parse("/spec/stream/offset"), JsonValue.Create(offset));
        return resources.PatchAsync<Subscription>(new Patch(PatchType.JsonPatch, new JsonPatch(operation)), subscription.GetName(), subscription.GetNamespace(), null, false, cancellationToken);
    }

}
