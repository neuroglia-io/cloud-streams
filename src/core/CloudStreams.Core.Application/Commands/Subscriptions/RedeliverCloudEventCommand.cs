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
using FluentValidation;
using Neuroglia.Data.Expressions.Services;
using Neuroglia.Data.Infrastructure.EventSourcing;
using Neuroglia.Serialization;
using System.Globalization;

namespace CloudStreams.Core.Application.Commands.Subscriptions;

/// <summary>
/// Represents the command used to deliver a recorded cloud event to the subscriber of a <see cref="Subscription"/> again, once (see #222)
/// </summary>
/// <param name="name">The name of the <see cref="Subscription"/> to redeliver the cloud event to</param>
/// <param name="namespace">The namespace the resource belongs to, if any</param>
/// <param name="request">An object that describes the cloud event to redeliver</param>
public class RedeliverCloudEventCommand(string name, CloudEventRedeliveryRequest request, string? @namespace = null)
    : Command<CloudEventRedelivery>
{

    /// <summary>
    /// Gets the namespace the resource belongs to
    /// </summary>
    public virtual string Namespace { get; } = string.IsNullOrWhiteSpace(@namespace) ? Neuroglia.Data.Infrastructure.ResourceOriented.Namespace.DefaultNamespaceName : @namespace;

    /// <summary>
    /// Gets the name of the <see cref="Subscription"/> to redeliver the cloud event to
    /// </summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentNullException(nameof(name)) : name;

    /// <summary>
    /// Gets an object that describes the cloud event to redeliver
    /// </summary>
    public CloudEventRedeliveryRequest Request { get; } = request ?? throw new ArgumentNullException(nameof(request));

}

/// <summary>
/// Represents the service used to handle <see cref="RedeliverCloudEventCommand"/>s
/// </summary>
/// <param name="resources">The service used to manage resources</param>
/// <param name="eventStore">The service used to read the cloud event to redeliver</param>
/// <param name="expressionEvaluator">The service used to evaluate the subscription's mutation, if any</param>
/// <param name="validators">The services used to validate mutated cloud events</param>
/// <param name="httpClientFactory">The service used to create the <see cref="HttpClient"/> that calls mutation webhooks</param>
/// <param name="delivery">The service used to deliver the cloud event to the subscriber's endpoint or sink, over any transport (see #218)</param>
/// <param name="serializer">The service used to read the cloud events mutation webhooks respond with</param>
/// <remarks>
/// The cloud event is read from the stream, or from the partition its offset refers to, mutated as the subscription mutates cloud events, and delivered to the subscriber
/// once, without an acknowledgement token: it neither goes through the subscription's filter nor moves its offset
/// </remarks>
public class RedeliverCloudEventCommandHandler(IResourceRepository resources, ICloudEventStore eventStore, IExpressionEvaluator expressionEvaluator, IEnumerable<IValidator<CloudEvent>> validators,
    IHttpClientFactory httpClientFactory, IJsonSerializer serializer, CloudEventDeliveryClient delivery)
    : ICommandHandler<RedeliverCloudEventCommand, CloudEventRedelivery>
{

    /// <summary>
    /// Gets the maximum amount of time to wait for the subscriber, or the mutation webhook, to respond
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<CloudEventRedelivery>> HandleAsync(RedeliverCloudEventCommand command, CancellationToken cancellationToken = default)
    {
        var subscription = await resources.GetAsync<Subscription>(command.Name, command.Namespace, cancellationToken).ConfigureAwait(false);
        if (subscription == null) return this.NotFound();
        var partition = command.Request.Partition;
        var records = partition == null
            ? eventStore.ReadAsync(subscription.GetNamespace(), StreamReadDirection.Forwards, (long)command.Request.Offset, 1, cancellationToken)
            : eventStore.ReadPartitionAsync(subscription.GetNamespace(), partition, StreamReadDirection.Forwards, (long)command.Request.Offset, 1, cancellationToken);
        var record = await records.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (record == null || record.Sequence != command.Request.Offset)
            return new OperationResult<CloudEventRedelivery>((int)HttpStatusCode.NotFound, errors: [new Error(ProblemTypes.NotFound, "Not Found", (int)HttpStatusCode.NotFound, $"There is no cloud event at offset '{command.Request.Offset}' of the {(partition == null ? "stream" : $"partition '{partition.Id}'")}")]);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        using var client = httpClientFactory.CreateClient();
        var pipeline = new SubscriptionEventPipeline(expressionEvaluator, validators, client, serializer);
        var e = record.ToCloudEvent(default);
        HttpStatusCode status;
        try
        {
            e = await pipeline.MutateAsync(subscription, e, timeout.Token).ConfigureAwait(false);
            KeyValuePair<string, string>[] headers = [new(CloudStreamsHeaders.Subscription, subscription.GetName()), new(CloudStreamsHeaders.Offset, record.Sequence.ToString(CultureInfo.InvariantCulture))];
            status = await delivery.SendAsync(DeliveryTarget.Of(subscription.Spec.Subscriber, subscription.GetNamespace()), e, headers, "The subscriber", timeout.Token).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            return new OperationResult<CloudEventRedelivery>((int)HttpStatusCode.BadGateway, errors: [new Error(RedeliverDeadLetterCommandHandler.RedeliveryFailedProblemType, "Bad Gateway", (int)HttpStatusCode.BadGateway, $"The redelivery of the cloud event at offset '{record.Sequence}' failed: {ex.Message}")]);
        }
        catch (Exception ex) when (ex is HttpRequestException or FormatException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            var reason = ex is OperationCanceledException ? $"the subscriber did not respond within {Timeout}" : ex.Message;
            return new OperationResult<CloudEventRedelivery>((int)HttpStatusCode.BadGateway, errors: [new Error(RedeliverDeadLetterCommandHandler.RedeliveryFailedProblemType, "Bad Gateway", (int)HttpStatusCode.BadGateway, $"Failed to redeliver the cloud event at offset '{record.Sequence}': {reason}")]);
        }
        return this.Ok(new CloudEventRedelivery() { Id = e.Id, Status = (int)status });
    }

}
