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

using System.Diagnostics;

namespace CloudStreams.UnitTests.Services;

/// <summary>
/// Represents an <see cref="HttpMessageHandler"/> used to script subscriber responses and record the resulting attempts
/// </summary>
/// <param name="responder">A function used to produce the response for the specified attempt. The attempt index is zero-based</param>
public class StubHttpMessageHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> responder)
    : HttpMessageHandler
{

    readonly Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> _responder = responder;
    readonly List<RecordedAttempt> _attempts = [];
    readonly Lock _lock = new();
    Stopwatch? _stopwatch;

    /// <summary>
    /// Gets the attempts recorded so far, in order
    /// </summary>
    public IReadOnlyList<RecordedAttempt> Attempts { get { lock (this._lock) return [.. this._attempts]; } }

    /// <summary>
    /// Gets the total amount of attempts recorded so far
    /// </summary>
    public int AttemptCount { get { lock (this._lock) return this._attempts.Count; } }

    /// <summary>
    /// Gets the interval between each attempt and the one before it. The first entry is always <see cref="TimeSpan.Zero"/>
    /// </summary>
    /// <remarks>
    /// This, rather than <see cref="RecordedAttempt.Elapsed"/>, is what a backoff assertion wants: it is immune to however long setup took.
    /// </remarks>
    public IReadOnlyList<TimeSpan> IntervalsBetweenAttempts
    {
        get
        {
            var attempts = this.Attempts;
            return [.. attempts.Select((attempt, index) => index == 0 ? TimeSpan.Zero : attempt.Elapsed - attempts[index - 1].Elapsed)];
        }
    }

    /// <summary>
    /// Clears everything recorded so far, and restarts the clock
    /// </summary>
    public void Reset() { lock (this._lock) { this._attempts.Clear(); this._stopwatch = null; } }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        int index;
        lock (this._lock)
        {
            // started on the first attempt rather than at construction, so setup time is never mistaken for backoff
            this._stopwatch ??= Stopwatch.StartNew();
            index = this._attempts.Count;
            this._attempts.Add(new(index, request.RequestUri, this._stopwatch.Elapsed));
        }
        return await this._responder(request, index, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a handler that always responds with the specified status code
    /// </summary>
    public static StubHttpMessageHandler AlwaysResponds(HttpStatusCode statusCode) => new((_, _, _) => Task.FromResult(new HttpResponseMessage(statusCode)));

    /// <summary>
    /// Creates a handler that walks the specified status codes, then keeps responding with the last one
    /// </summary>
    public static StubHttpMessageHandler Responds(params HttpStatusCode[] statusCodes)
    {
        ArgumentOutOfRangeException.ThrowIfZero(statusCodes.Length);
        return new((_, attempt, _) => Task.FromResult(new HttpResponseMessage(statusCodes[Math.Min(attempt, statusCodes.Length - 1)])));
    }

    /// <summary>
    /// Creates a handler that fails the specified amount of times before succeeding
    /// </summary>
    public static StubHttpMessageHandler FailsThenSucceeds(int failures, HttpStatusCode failureStatusCode = HttpStatusCode.ServiceUnavailable) =>
        new((_, attempt, _) => Task.FromResult(new HttpResponseMessage(attempt < failures ? failureStatusCode : HttpStatusCode.OK)));

    /// <summary>
    /// Creates a handler that always times out, the way <see cref="HttpClient"/> does when its timeout elapses
    /// </summary>
    /// <remarks>
    /// <see cref="HttpClient"/> surfaces a timeout as a <see cref="TaskCanceledException"/> wrapping a <see cref="TimeoutException"/>, and does so
    /// without the caller's own <see cref="CancellationToken"/> being canceled. That distinction is the whole point of the stub - see issue #132.
    /// </remarks>
    public static StubHttpMessageHandler AlwaysTimesOut() => new((_, _, _) => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.", new TimeoutException()));

    /// <summary>
    /// Creates an <see cref="HttpClient"/> backed by this handler
    /// </summary>
    public HttpClient CreateClient() => new(this, disposeHandler: false);

    /// <summary>
    /// Describes a single recorded dispatch attempt
    /// </summary>
    /// <param name="Index">The zero-based index of the attempt</param>
    /// <param name="RequestUri">The uri the attempt was sent to</param>
    /// <param name="Elapsed">The time elapsed since the <em>first</em> attempt, so setup time is never counted. Zero for the first attempt. Prefer <see cref="IntervalsBetweenAttempts"/> when asserting backoff</param>
    public record RecordedAttempt(int Index, Uri? RequestUri, TimeSpan Elapsed);

}
