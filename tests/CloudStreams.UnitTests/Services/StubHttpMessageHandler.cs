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

namespace CloudStreams.UnitTests.Services;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that returns scripted responses and records every request it receives
/// </summary>
public class StubHttpMessageHandler
    : HttpMessageHandler
{

    /// <summary>
    /// The number of requests the handler serves by default before failing the test
    /// </summary>
    public const int DefaultMaxRequests = 50;

    readonly Lock _lock = new();
    readonly List<HttpStatusCode> _script = [];
    readonly List<HttpRequestMessage> _requests = [];
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _fallback = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    /// <summary>
    /// Gets/sets the total number of requests the handler will serve before failing the test.
    /// <para>
    /// This is a safety valve, not a behavioural limit: an unfixed
    /// <see href="https://github.com/neuroglia-io/cloud-streams/issues/131">#131</see> dispatches in an unbounded loop, which
    /// would otherwise hang the test host instead of failing fast. It counts every request the fixture serves, not the
    /// attempts made for one event, so a test that deliberately replays a large backlog must raise it.
    /// </para>
    /// </summary>
    public int MaxRequests { get; set; } = DefaultMaxRequests;

    /// <summary>
    /// Gets a list containing every request received by the handler, in order
    /// </summary>
    public IReadOnlyList<HttpRequestMessage> Requests { get { lock (this._lock) return [.. this._requests]; } }

    /// <summary>
    /// Gets the number of requests served by the handler
    /// </summary>
    public int RequestCount { get { lock (this._lock) return this._requests.Count; } }

    /// <summary>
    /// Configures the handler to respond with the specified status code to every request
    /// </summary>
    /// <param name="statusCode">The status code to respond with</param>
    /// <returns>The configured <see cref="StubHttpMessageHandler"/></returns>
    public StubHttpMessageHandler Respond(HttpStatusCode statusCode)
    {
        lock (this._lock) this._fallback = (_, _) => Task.FromResult(new HttpResponseMessage(statusCode));
        return this;
    }

    /// <summary>
    /// Configures the handler to respond with the specified status code and body to every request
    /// </summary>
    /// <param name="statusCode">The status code to respond with</param>
    /// <param name="content">The response body to respond with</param>
    /// <returns>The configured <see cref="StubHttpMessageHandler"/></returns>
    public StubHttpMessageHandler RespondWith(HttpStatusCode statusCode, string content)
    {
        lock (this._lock) this._fallback = (_, _) => Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(content) });
        return this;
    }

    /// <summary>
    /// Configures the handler to respond with the specified status codes, in order. Once the script is exhausted, the last status code is repeated
    /// </summary>
    /// <param name="statusCodes">The status codes to respond with, in order</param>
    /// <returns>The configured <see cref="StubHttpMessageHandler"/></returns>
    public StubHttpMessageHandler Script(params HttpStatusCode[] statusCodes)
    {
        lock (this._lock) this._script.AddRange(statusCodes);
        if (statusCodes.Length > 0) this.Respond(statusCodes[^1]);
        return this;
    }

    /// <summary>
    /// Configures the handler to delay for the specified duration while honouring the request's <see cref="CancellationToken"/>.
    /// Combined with a shorter <see cref="HttpClient.Timeout"/>, this reproduces the timeout described by
    /// <see href="https://github.com/neuroglia-io/cloud-streams/issues/132">#132</see>
    /// </summary>
    /// <param name="delay">The duration to delay for</param>
    /// <returns>The configured <see cref="StubHttpMessageHandler"/></returns>
    public StubHttpMessageHandler Delay(TimeSpan delay)
    {
        lock (this._lock)
        {
            this._fallback = async (_, cancellationToken) =>
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK);
            };
        }
        return this;
    }

    /// <summary>
    /// Configures the handler to throw the specified <see cref="Exception"/> instead of responding. Used to simulate a transport-level failure,
    /// which surfaces as an <see cref="HttpRequestException"/> with a null <see cref="HttpRequestException.StatusCode"/>
    /// </summary>
    /// <param name="exception">A function used to create the <see cref="Exception"/> to throw</param>
    /// <returns>The configured <see cref="StubHttpMessageHandler"/></returns>
    public StubHttpMessageHandler Throws(Func<Exception> exception)
    {
        lock (this._lock) this._fallback = (_, _) => throw exception();
        return this;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        int index;
        HttpStatusCode? scripted;
        //the handler dispatches from the Rx subscription callback and from the catch-up loop, so claiming an index and
        //recording the request has to be one atomic step or two concurrent sends replay the same scripted response
        lock (this._lock)
        {
            index = this._requests.Count;
            if (index >= this.MaxRequests) throw new InvalidOperationException($"The subscriber received more than {this.MaxRequests} requests. This is the runaway-dispatch safety valve: either the retry policy is not being honoured (see issue #131), or this test legitimately needs a higher StubHttpMessageHandler.MaxRequests.");
            this._requests.Add(request);
            scripted = index < this._script.Count ? this._script[index] : null;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (scripted.HasValue) return new HttpResponseMessage(scripted.Value);
        return await this._fallback(request, cancellationToken).ConfigureAwait(false);
    }

}
