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

using Neuroglia.Data.Infrastructure.EventSourcing.Services;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers the <see cref="GaplessStreamObservation"/>, which observes a stream without losing the records appended while the observation starts (#204)
/// </summary>
public class GaplessStreamObservationTests
{

    static IEventRecord Record(ulong offset)
    {
        var record = Substitute.For<IEventRecord>();
        record.Offset.Returns(offset);
        return record;
    }

    /// <summary>
    /// Creates an event store whose stream holds the specified records, and that appends the specified records while they are being read, as happens when events
    /// are published, or linked by a projection, while a subscription starts
    /// </summary>
    static (IEventStore Store, Subject<IEventRecord> Live) Store(ulong[] held, ulong[] appendedDuringRead)
    {
        var live = new Subject<IEventRecord>();
        var store = Substitute.For<IEventStore>();
        store.ObserveAsync(Arg.Any<string>(), StreamPosition.EndOfStream, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IObservable<IEventRecord>>(live));
        store.ReadAsync(Arg.Any<string>(), StreamReadDirection.Forwards, Arg.Any<long>(), Arg.Any<ulong?>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var offset = call.ArgAt<long>(2);
            foreach (var appended in appendedDuringRead) live.OnNext(Record(appended));
            // a read sees what the stream held, plus whatever was appended before it reached the end
            return held.Concat(appendedDuringRead).Where(o => (long)o >= offset).Select(Record).ToAsyncEnumerable();
        });
        return (store, live);
    }

    static async Task<List<ulong>> ObserveAsync(IEventStore store, long offset, Action? afterSubscription = null)
    {
        var observed = new List<ulong>();
        var observation = await GaplessStreamObservation.ObserveAsync(store, "stream", offset);
        using var subscription = observation.Subscribe(r => observed.Add(r.Offset));
        afterSubscription?.Invoke();
        return observed;
    }

    [Fact]
    public async Task Records_Appended_While_The_Observation_Starts_Should_Be_Observed_Once()
    {
        // arrange
        var (store, live) = Store([0, 1, 2, 3], [4]);

        // act
        var observed = await ObserveAsync(store, 0, () => { live.OnNext(Record(4)); live.OnNext(Record(5)); });

        // assert
        Assert.Equal([0ul, 1, 2, 3, 4, 5], observed);
    }

    [Fact]
    public async Task Records_Appended_Between_The_Read_And_The_Subscription_Should_Be_Observed()
    {
        // arrange
        var (store, live) = Store([0, 1, 2], []);
        var observation = await GaplessStreamObservation.ObserveAsync(store, "stream", 1);
        live.OnNext(Record(3));
        var observed = new List<ulong>();

        // act
        using var subscription = observation.Subscribe(r => observed.Add(r.Offset));
        live.OnNext(Record(4));

        // assert
        Assert.Equal([1ul, 2, 3, 4], observed);
    }

    [Fact]
    public async Task Observing_An_Empty_Stream_Should_Yield_Every_Record_Appended_Afterwards()
    {
        // arrange
        var (store, live) = Store([], []);

        // act
        var observed = await ObserveAsync(store, 0, () => { live.OnNext(Record(0)); live.OnNext(Record(1)); });

        // assert
        Assert.Equal([0ul, 1], observed);
    }

    [Fact]
    public async Task A_Failure_Of_The_Live_Subscription_Before_Subscribing_Should_Be_Reported()
    {
        // arrange
        var (store, live) = Store([0], []);
        var observation = await GaplessStreamObservation.ObserveAsync(store, "stream", 0);
        live.OnError(new InvalidOperationException("dropped"));
        var observed = new List<ulong>();
        Exception? error = null;

        // act
        using var subscription = observation.Subscribe(r => observed.Add(r.Offset), ex => error = ex);

        // assert
        Assert.Equal([0ul], observed);
        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public async Task Disposing_The_Subscription_Should_Stop_The_Live_Subscription()
    {
        // arrange
        var (store, live) = Store([0], []);
        var observation = await GaplessStreamObservation.ObserveAsync(store, "stream", 0);

        // act
        observation.Subscribe(_ => { }).Dispose();

        // assert
        Assert.False(live.HasObservers);
    }

}
