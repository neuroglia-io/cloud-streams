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
/// <param name="offset">The offset of the dead letter in the subscription's dead-letter stream</param>
public class RedeliverDeadLetterCommand(string name, ulong offset)
    : Command<DeadLetterRedelivery>
{

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
/// <param name="httpClientFactory">The service used to create the <see cref="HttpClient"/> that redelivers dead letters</param>
/// <remarks>
/// The dead letter is delivered as it was to be delivered the first time, mutated if the subscription mutates cloud events, but without an acknowledgement token: it is
/// delivered once, and its redelivery neither moves the subscription's offset nor removes the dead letter
/// </remarks>
public class RedeliverDeadLetterCommandHandler(IResourceRepository resources, IDeadLetterStore deadLetters, IHttpClientFactory httpClientFactory)
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
        var subscription = await resources.GetAsync<Subscription>(command.Name, null, cancellationToken).ConfigureAwait(false);
        if (subscription == null) return this.NotFound();
        var deadLetter = await deadLetters.ReadAsync(subscription.GetQualifiedName(), StreamReadDirection.Forwards, (long)command.Offset, 1, cancellationToken).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (deadLetter == null || deadLetter.Offset != command.Offset)
            return new OperationResult<DeadLetterRedelivery>((int)HttpStatusCode.NotFound, errors: [new Error(ProblemTypes.NotFound, "Not Found", (int)HttpStatusCode.NotFound, $"The subscription '{command.Name}' has no dead letter at offset '{command.Offset}'")]);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        HttpStatusCode status;
        string? content;
        try
        {
            using var client = httpClientFactory.CreateClient();
            using var requestContent = deadLetter.Event.ToHttpContent();
            using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Spec.Subscriber.Uri) { Content = requestContent };
            request.Headers.TryAddWithoutValidation(CloudStreamsHeaders.Subscription, subscription.GetName());
            request.Headers.TryAddWithoutValidation(CloudStreamsHeaders.Offset, deadLetter.EventOffset.ToString(CultureInfo.InvariantCulture));
            using var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
            status = response.StatusCode;
            content = response.IsSuccessStatusCode ? null : await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            var reason = ex is OperationCanceledException ? $"the subscriber did not respond within {Timeout}" : ex.Message;
            return new OperationResult<DeadLetterRedelivery>((int)HttpStatusCode.BadGateway, errors: [new Error(RedeliveryFailedProblemType, "Bad Gateway", (int)HttpStatusCode.BadGateway, $"Failed to redeliver the dead letter at offset '{command.Offset}': {reason}")]);
        }
        if ((int)status is < 200 or >= 300)
        {
            var detail = $"The subscriber responded to the redelivery of the dead letter at offset '{command.Offset}' with a non-success status code '{(int)status}'";
            if (!string.IsNullOrWhiteSpace(content)) detail = $"{detail}: {content}";
            return new OperationResult<DeadLetterRedelivery>((int)HttpStatusCode.BadGateway, errors: [new Error(RedeliveryFailedProblemType, "Bad Gateway", (int)HttpStatusCode.BadGateway, detail)]);
        }
        return this.Ok(new DeadLetterRedelivery() { Offset = command.Offset, Status = (int)status });
    }

}
