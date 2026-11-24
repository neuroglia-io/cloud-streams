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

namespace CloudStreams.UnitTests.Cases.Broker;

/// <summary>
/// Pins what a subscriber timeout does today, which is issue #132 and is <em>not</em> the desired behaviour
/// </summary>
/// <remarks>
/// <para>
/// These are characterisation tests. They assert the broken behaviour on purpose, so it is visible in the suite
/// rather than buried in a document, and so they fail loudly the moment the fix lands.
/// </para>
/// <para>
/// <b>PR 3 (#132) must invert every assertion in this file</b> and rename the tests accordingly. A timeout should
/// be routed through the retry path like any other delivery failure, the event should be redelivered rather than
/// skipped, and the failure should be visible in the status and the metrics.
/// </para>
/// <para>
/// The root cause: <see cref="HttpClient"/> raises a timeout as <see cref="TaskCanceledException"/>, not
/// <see cref="HttpRequestException"/>, so it slips past the retry path entirely and is then absorbed by the
/// cancellation filters that exist to swallow genuine shutdown.
/// </para>
/// </remarks>
public class SubscriptionHandlerTimeoutTests
{

    /// <summary>
    /// Live path. The timeout is swallowed by the cancellation filter in <c>OnCloudEventAsync</c>: no offset is committed,
    /// no retry is scheduled, and nothing is recorded. The next event then commits its own higher offset, stepping
    /// <c>ackedOffset</c> past the timed-out event so it is never redelivered.
    /// </summary>
    [Fact]
    public async Task Live_Dispatch_Timeout_Is_Currently_Swallowed_And_The_Event_Is_Skipped()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysTimesOut());

        // act
        var exception = await Record.ExceptionAsync(() => context.Handler.HandleCloudEventAsync(BrokerTestContext.CreateRecord(41)));

        // assert - BROKEN BEHAVIOUR, see the remarks on this class
        Assert.Null(exception);
        Assert.Equal(1, context.Http.AttemptCount);                     // #132 fix: should be retried, so more than one
        Assert.Equal(0, context.Metrics.TotalDeliveryFailures);         // #132 fix: should count as a delivery failure
        Assert.Equal(0, context.Patches.Count);                         // #132 fix: should surface a reason on the status
        Assert.False(context.Patches.Touched("/status/stream/ackedOffset"));
    }

    /// <summary>
    /// A timeout is not an <see cref="HttpRequestException"/>, so <c>DispatchAsync</c> does not recognise it as a delivery
    /// failure and lets it escape instead of routing it into <c>RetryDispatchAsync</c>. This is the mechanism behind both
    /// the skipped event above and the wedged catch-up.
    /// </summary>
    [Fact]
    public async Task Dispatch_Timeout_Currently_Escapes_Instead_Of_Entering_The_Retry_Path()
    {
        // arrange
        await using var context = await BrokerTestContext.CreateAsync(StubHttpMessageHandler.AlwaysTimesOut());

        // act
        var exception = await Record.ExceptionAsync(() => context.Handler.DispatchRecordAsync(BrokerTestContext.CreateRecord(41)));

        // assert - BROKEN BEHAVIOUR, see the remarks on this class
        Assert.IsAssignableFrom<OperationCanceledException>(exception);  // #132 fix: should not escape at all
        Assert.Equal(1, context.Http.AttemptCount);
        // the handler's own token is NOT cancelled, which is exactly how the fix can tell a timeout from a real shutdown
        Assert.IsType<TimeoutException>(exception!.InnerException);
    }

}
