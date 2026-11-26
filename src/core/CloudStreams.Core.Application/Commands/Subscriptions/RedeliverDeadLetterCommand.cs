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

using CloudStreams.Core.Application.Services;
using Neuroglia.Data.Infrastructure.EventSourcing;
using System.Globalization;

namespace CloudStreams.Core.Application.Commands.Subscriptions;

/// <summary>
/// Represents the command used to deliver a dead letter of a <see cref="Subscription"/> to its subscriber again, once (see #207)
/// </summary>
/// <param name="name">The name of the <see cref="Subscription"/> the dead letter belongs to</param>
/// <param name="namespace">The namespace the resource belongs to, if any</param>
/// <param name="offset">The offset of the dead letter in the subscription's dead-letter stream</param>
public class RedeliverDeadLetterCommand(string name, ulong offset, string? @namespace = null)
    : Command<DeadLetterRedelivery>
{

    /// <summary>
    /// Gets the namespace the resource belongs to
    /// </summary>
    public virtual string Namespace { get; } = string.IsNullOrWhiteSpace(@namespace) ? Neuroglia.Data.Infrastructure.ResourceOriented.Namespace.DefaultNamespaceName : @namespace;

    /// <summary>
    /// Gets the name of the <see cref="Subscription"/> the dead letter belongs to
    /// </summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentNullException(nameof(name)) : name;

    /// <summary>
    /// Gets the offset of the dead letter in the subscription's dead-letter stream
    /// </summary>
    public ulong Offset { get; } = offset;

}

/// <summary>
/// Represents the service used to handle <see cref="RedeliverDeadLetterCommand"/>s
/// </summary>
/// <param name="resources">The service used to manage resources</param>
/// <param name="deadLetters">The service used to store dead letters</param>
/// <param name="delivery">The service used to deliver dead letters to the subscriber's endpoint or sink, over any transport (see #218)</param>
/// <remarks>
/// The dead letter is delivered as it was to be delivered the first time, mutated if the subscription mutates cloud events, but without an acknowledgement token: it is
/// delivered once, and its redelivery neither moves the subscription's offset nor removes the dead letter
/// </remarks>
public class RedeliverDeadLetterCommandHandler(IResourceRepository resources, IDeadLetterStore deadLetters, CloudEventDeliveryClient delivery)
    : ICommandHandler<RedeliverDeadLetterCommand, DeadLetterRedelivery>
{

    /// <summary>
    /// Gets the type of the problem that describes a dead letter its subscriber refused again
    /// </summary>
    public static readonly Uri RedeliveryFailedProblemType = new("https://cloud-streams.io/problems/redelivery-failed");

    /// <summary>
    /// Gets the maximum amount of time to wait for the subscriber to respond
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<DeadLetterRedelivery>> HandleAsync(RedeliverDeadLetterCommand command, CancellationToken cancellationToken = default)
    {
        var subscription = await resources.GetAsync<Subscription>(command.Name, command.Namespace, cancellationToken).ConfigureAwait(false);
        if (subscription == null) return this.NotFound();
        var deadLetter = await deadLetters.ReadAsync(subscription.GetQualifiedName(), StreamReadDirection.Forwards, (long)command.Offset, 1, cancellationToken).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (deadLetter == null || deadLetter.Offset != command.Offset)
            return new OperationResult<DeadLetterRedelivery>((int)HttpStatusCode.NotFound, errors: [new Error(ProblemTypes.NotFound, "Not Found", (int)HttpStatusCode.NotFound, $"The subscription '{command.Name}' has no dead letter at offset '{command.Offset}'")]);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        HttpStatusCode status;
        try
        {
            KeyValuePair<string, string>[] headers = [new(CloudStreamsHeaders.Subscription, subscription.GetName()), new(CloudStreamsHeaders.Offset, deadLetter.EventOffset.ToString(CultureInfo.InvariantCulture))];
            status = await delivery.SendAsync(DeliveryTarget.Of(subscription.Spec.Subscriber, subscription.GetNamespace()), deadLetter.Event, headers, "The subscriber", timeout.Token).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            return new OperationResult<DeadLetterRedelivery>((int)HttpStatusCode.BadGateway, errors: [new Error(RedeliveryFailedProblemType, "Bad Gateway", (int)HttpStatusCode.BadGateway, $"The redelivery of the dead letter at offset '{command.Offset}' failed: {ex.Message}")]);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            var reason = ex is OperationCanceledException ? $"the subscriber did not respond within {Timeout}" : ex.Message;
            return new OperationResult<DeadLetterRedelivery>((int)HttpStatusCode.BadGateway, errors: [new Error(RedeliveryFailedProblemType, "Bad Gateway", (int)HttpStatusCode.BadGateway, $"Failed to redeliver the dead letter at offset '{command.Offset}': {reason}")]);
        }
        return this.Ok(new DeadLetterRedelivery() { Offset = command.Offset, Status = (int)status });
    }

}
