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
using System.Runtime.CompilerServices;

namespace CloudStreams.Core.Application.Queries.Subscriptions;

/// <summary>
/// Represents the query used to run a subscription's filter and mutation against sample or recorded cloud events, without delivering anything (see #224)
/// </summary>
/// <param name="namespace">The namespace of the subscription to test</param>
/// <param name="request">The object that describes the test to run</param>
public class TestSubscriptionQuery(SubscriptionTestRequest request, string? @namespace = null)
    : Query<SubscriptionTestResult>
{

    /// <summary>
    /// Gets the namespace the resource belongs to
    /// </summary>
    public virtual string Namespace { get; } = string.IsNullOrWhiteSpace(@namespace) ? Neuroglia.Data.Infrastructure.ResourceOriented.Namespace.DefaultNamespaceName : @namespace;

    /// <summary>
    /// Gets the object that describes the test to run
    /// </summary>
    public SubscriptionTestRequest Request { get; } = request ?? throw new ArgumentNullException(nameof(request));

}

/// <summary>
/// Represents the service used to handle <see cref="TestSubscriptionQuery"/> instances
/// </summary>
/// <param name="eventStore">The service used to read the recorded cloud events to test</param>
/// <param name="expressionEvaluator">The service used to evaluate the filter and mutation expressions</param>
/// <param name="validators">The services used to validate mutated cloud events</param>
/// <param name="serializer">The service used to serialize and deserialize JSON</param>
public class TestSubscriptionQueryHandler(ICloudEventStore eventStore, IExpressionEvaluator expressionEvaluator, IEnumerable<IValidator<CloudEvent>> validators, IJsonSerializer serializer)
    : IQueryHandler<TestSubscriptionQuery, SubscriptionTestResult>
{

    /// <summary>
    /// Gets the maximum amount of time a test may run for
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the notice returned when the tested mutation is a webhook
    /// </summary>
    public const string WebhookNotice = "Mutation webhooks are not called by tests, which never send anything: the cloud events shown are not mutated";

    static readonly Uri ValidationFailedProblemType = new("https://cloud-streams.io/problems/validation-failed");

    /// <inheritdoc/>
    /// <remarks>Each cloud event is tested on its own: an expression that fails for one of them is reported with it, and does not stop the others</remarks>
    public virtual async Task<IOperationResult<SubscriptionTestResult>> HandleAsync(TestSubscriptionQuery query, CancellationToken cancellationToken)
    {
        var request = query.Request;
        var length = request.Length ?? SubscriptionTestRequest.DefaultLength;
        if (length < 1 || length > SubscriptionTestRequest.MaxLength) return Invalid($"The amount of cloud events to test must be between 1 and {SubscriptionTestRequest.MaxLength}");
        if (request.Events?.Count > SubscriptionTestRequest.MaxLength) return Invalid($"At most {SubscriptionTestRequest.MaxLength} sample cloud events may be tested");
        if (request.Filter?.Type == CloudEventFilterType.Attributes && request.Filter.Attributes == null) return Invalid("An attribute filter must define the attributes to filter on");
        if (request.Filter?.Type == CloudEventFilterType.Expression && string.IsNullOrWhiteSpace(request.Filter.Expression)) return Invalid("An expression filter must define its expression");
        if (request.Mutation?.Type == CloudEventMutationType.Expression && request.Mutation.Expression == null) return Invalid("An expression mutation must define its expression");
        var webhook = request.Mutation?.Type == CloudEventMutationType.Webhook;
        var subscription = new Subscription()
        {
            Metadata = new() { Name = "test" },
            Spec = new()
            {
                Subscriber = new() { Endpoint = new Uri("http://localhost") },
                Filter = request.Filter,
                Mutation = webhook ? null : request.Mutation
            }
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        // nothing is sent: the client is only there because the pipeline requires one
        using var client = new HttpClient();
        var pipeline = new SubscriptionEventPipeline(expressionEvaluator, validators, client, serializer);
        var result = new SubscriptionTestResult() { Notice = webhook ? WebhookNotice : null };
        await foreach (var (record, recorded) in this.GetRecordsAsync(query.Namespace, request, length, timeout.Token).ConfigureAwait(false))
        {
            var item = new SubscriptionTestResultItem() { Offset = recorded ? record.Sequence : null, Event = record.ToCloudEvent() };
            try
            {
                item.Matched = await pipeline.FiltersAsync(subscription, record, timeout.Token).ConfigureAwait(false);
                if (item.Matched && subscription.Spec.Mutation != null) item.Mutated = await pipeline.MutateAsync(subscription, item.Event, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                item.Error = ex.Message;
            }
            result.Items.Add(item);
        }
        return this.Ok(result);
    }

    /// <summary>
    /// Gets the records to test: the sample cloud events, if any, otherwise the most recent cloud events of the partition or of the stream
    /// </summary>
    /// <param name="namespace">The namespace of the subscription to test</param>
    /// <param name="request">The object that describes the test to run</param>
    /// <param name="length">The amount of recorded cloud events to read</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The records to test, and whether they have been read from the store</returns>
    protected virtual async IAsyncEnumerable<(CloudEventRecord Record, bool Recorded)> GetRecordsAsync(string @namespace, SubscriptionTestRequest request, int length, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Events != null)
        {
            for (var i = 0; i < request.Events.Count; i++)
            {
                var e = request.Events[i];
                yield return (new CloudEventRecord("sample", (ulong)i, new CloudEventMetadata(e.GetContextAttributes().ToDictionary(kvp => kvp.Key, kvp => kvp.Value)), e.Data), false);
            }
            yield break;
        }
        var records = request.Partition == null
            ? eventStore.ReadAsync(@namespace, StreamReadDirection.Backwards, StreamPosition.EndOfStream, (ulong)length, cancellationToken)
            : eventStore.ReadPartitionAsync(@namespace, request.Partition, StreamReadDirection.Backwards, StreamPosition.EndOfStream, (ulong)length, cancellationToken);
        await foreach (var record in records.ConfigureAwait(false)) yield return (record, true);
    }

    static OperationResult<SubscriptionTestResult> Invalid(string detail) => new((int)HttpStatusCode.BadRequest, errors: [new Error(ValidationFailedProblemType, "Validation Failed", (int)HttpStatusCode.BadRequest, detail)]);

}
