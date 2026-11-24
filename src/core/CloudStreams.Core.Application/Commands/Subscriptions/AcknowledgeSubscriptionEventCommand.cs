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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace CloudStreams.Core.Application.Commands.Subscriptions;

/// <summary>
/// Represents the command used to resolve a cloud event whose acknowledgement a subscriber has deferred, by acknowledging or negatively acknowledging it
/// </summary>
/// <param name="name">The name of the subscription the cloud event has been delivered for</param>
/// <param name="token">The acknowledgement token the cloud event has been delivered with</param>
/// <param name="acknowledge">A boolean indicating whether to acknowledge the cloud event (it is then considered processed) or to negatively acknowledge it (it is then redelivered)</param>
/// <param name="reason">The reason why the cloud event is negatively acknowledged, if any</param>
public class AcknowledgeSubscriptionEventCommand(string name, string token, bool acknowledge, string? reason = null)
    : Command<Subscription>
{

    /// <summary>
    /// Gets the name of the subscription the cloud event has been delivered for
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the acknowledgement token the cloud event has been delivered with
    /// </summary>
    public string Token { get; } = token;

    /// <summary>
    /// Gets a boolean indicating whether to acknowledge the cloud event or to negatively acknowledge it
    /// </summary>
    public bool Acknowledge { get; } = acknowledge;

    /// <summary>
    /// Gets the reason why the cloud event is negatively acknowledged, if any
    /// </summary>
    public string? Reason { get; } = reason;

}

/// <summary>
/// Represents the service used to handle <see cref="AcknowledgeSubscriptionEventCommand"/>s
/// </summary>
/// <param name="resources">The service used to manage resources</param>
/// <param name="gracePeriod">How long a token that matches no pending acknowledgement is given to become pending, before it is refused. Defaults to 5 seconds</param>
/// <remarks>
/// The acknowledgement token is the only authorization required, so it is compared in fixed time, and it is single-use: the status patch only applies if the
/// acknowledgement it resolves is still pending. The broker, which watches the subscription, resumes delivery as soon as the pending acknowledgement is gone:
/// after the acknowledged offset on acknowledgement, or at the same offset (which redelivers the event) on negative acknowledgement
/// </remarks>
public class AcknowledgeSubscriptionEventCommandHandler(IResourceRepository resources, TimeSpan? gracePeriod = null)
    : ICommandHandler<AcknowledgeSubscriptionEventCommand, Subscription>
{

    /// <summary>
    /// Gets the type of the problem returned when the specified token does not match a pending acknowledgement
    /// </summary>
    public static readonly Uri AcknowledgementConflictProblemType = new("https://cloud-streams.io/problems/acknowledgement-conflict");

    /// <summary>
    /// Gets the default duration of the grace period given to a token to become pending
    /// </summary>
    public static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the interval at which a token is checked again during its grace period
    /// </summary>
    static readonly TimeSpan GracePollingInterval = TimeSpan.FromMilliseconds(100);

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<Subscription>> HandleAsync(AcknowledgeSubscriptionEventCommand command, CancellationToken cancellationToken = default)
    {
        // the mediator turns exceptions into internal server errors: the problems that describe why an acknowledgement is refused are returned instead
        try
        {
            return await this.AcknowledgeAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (ProblemDetailsException ex) when (ex.Problem.Status is >= 400 and < 500)
        {
            return new OperationResult<Subscription>(ex.Problem.Status, errors: [new Error(ex.Problem.Type, ex.Problem.Title, ex.Problem.Status, ex.Problem.Detail)]);
        }
    }

    /// <summary>
    /// Acknowledges or negatively acknowledges the cloud event described by the specified command
    /// </summary>
    /// <param name="command">The command to handle</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IOperationResult{TResult}"/></returns>
    /// <exception cref="ProblemDetailsException">The acknowledgement is invalid, or does not match a pending one</exception>
    protected virtual async Task<IOperationResult<Subscription>> AcknowledgeAsync(AcknowledgeSubscriptionEventCommand command, CancellationToken cancellationToken)
    {
        using var activity = CloudStreamsDefaults.Telemetry.ActivitySource.StartActivity(command.Acknowledge ? "AcknowledgeSubscriptionEvent" : "NegativelyAcknowledgeSubscriptionEvent");
        activity?.SetTag("subscription", command.Name);
        if (string.IsNullOrWhiteSpace(command.Token)) throw new ProblemDetailsException(new(new Uri("https://cloud-streams.io/problems/validation-failed"), "Validation Failed", (int)HttpStatusCode.BadRequest, "The acknowledgement token is required"));
        var deadline = DateTimeOffset.UtcNow + (gracePeriod ?? DefaultGracePeriod);
        Subscription? subscription;
        PendingAcknowledgement? pendingAck;
        while (true)
        {
            subscription = null;
            try { subscription = await resources.GetAsync<Subscription>(command.Name, null, cancellationToken).ConfigureAwait(false); }
            catch (ProblemDetailsException ex) when (ex.Problem.Status == (int)HttpStatusCode.NotFound) { }
            pendingAck = subscription?.Status?.Subscriber?.PendingAck;
            if (subscription != null && pendingAck != null && AcknowledgementTokens.Matches(command.Token, pendingAck.Token)) break;
            // a token issued for a delivery whose '202 Accepted' response the broker has yet to record is given time to become pending
            if (DateTimeOffset.UtcNow >= deadline) break;
            await Task.Delay(GracePollingInterval, cancellationToken).ConfigureAwait(false);
        }
        // an unknown subscription is reported like an invalid token, so that the endpoint, which is anonymous, does not reveal which subscriptions exist (see #149)
        if (subscription == null || pendingAck == null || !AcknowledgementTokens.Matches(command.Token, pendingAck.Token)) throw Conflict("The specified acknowledgement token does not match any pending acknowledgement: it is unknown, has already been used, or has expired");
        if (pendingAck.ExpiresAt <= DateTimeOffset.UtcNow) throw Conflict($"The acknowledgement of the cloud event at offset '{pendingAck.Offset}' expired at {pendingAck.ExpiresAt:O}: the cloud event is being redelivered");
        var operations = new List<PatchOperation>()
        {
            PatchOperation.Test(JsonPointer.Parse("/status/subscriber/pendingAck/token"), JsonValue.Create(pendingAck.Token)),
            PatchOperation.Remove(JsonPointer.Parse("/status/subscriber/pendingAck")),
            PatchOperation.Add(JsonPointer.Parse("/status/subscriber/lastAcknowledgedAt"), JsonValue.Create(DateTimeOffset.UtcNow))
        };
        if (command.Acknowledge)
        {
            if (subscription.Status!.Stream == null) operations.Add(PatchOperation.Add(JsonPointer.Parse("/status/stream"), new JsonObject()));
            operations.Add(PatchOperation.Add(JsonPointer.Parse("/status/stream/ackedOffset"), JsonValue.Create(pendingAck.Offset + 1)));
            if (subscription.Status.Subscriber!.Reason != null) operations.Add(PatchOperation.Remove(JsonPointer.Parse("/status/subscriber/reason")));
        }
        else
        {
            var reason = $"The subscriber negatively acknowledged the cloud event at offset '{pendingAck.Offset}'";
            if (!string.IsNullOrWhiteSpace(command.Reason)) reason = $"{reason}: {command.Reason}";
            operations.Add(PatchOperation.Add(JsonPointer.Parse("/status/subscriber/reason"), JsonValue.Create(reason)));
        }
        try
        {
            subscription = await resources.PatchStatusAsync<Subscription>(new Patch(PatchType.JsonPatch, new JsonPatch(operations)), command.Name, null, null, false, cancellationToken).ConfigureAwait(false);
        }
        catch (ProblemDetailsException ex) when (ex.Problem.Status is (int)HttpStatusCode.BadRequest or (int)HttpStatusCode.Conflict)
        {
            // the 'test' operation failed: the acknowledgement has been resolved, or has expired, in the meantime
            throw Conflict("The acknowledgement has been resolved or has expired in the meantime");
        }
        return this.Ok(subscription);
    }

    static ProblemDetailsException Conflict(string detail) => new(new(AcknowledgementConflictProblemType, "Acknowledgement Conflict", (int)HttpStatusCode.Conflict, detail));


}
