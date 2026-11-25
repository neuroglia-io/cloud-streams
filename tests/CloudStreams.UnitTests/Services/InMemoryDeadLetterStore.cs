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
/// Represents an <see cref="IDeadLetterStore"/> that keeps dead letters in memory, and can be made to fail
/// </summary>
public sealed class InMemoryDeadLetterStore
    : IDeadLetterStore
{

    readonly Lock _lock = new();
    readonly List<(string Subscription, DeadLetter DeadLetter)> _deadLetters = [];

    /// <summary>
    /// Gets the dead letters appended so far
    /// </summary>
    public IReadOnlyList<DeadLetter> All { get { lock (this._lock) return [.. this._deadLetters.Select(d => d.DeadLetter)]; } }

    /// <summary>
    /// Gets/sets the exception appending a dead letter throws, if any
    /// </summary>
    public Exception? Failure { get; set; }

    /// <inheritdoc/>
    public Task<ulong> AppendAsync(string subscription, DeadLetter deadLetter, CancellationToken cancellationToken = default)
    {
        if (this.Failure != null) return Task.FromException<ulong>(this.Failure);
        lock (this._lock)
        {
            deadLetter.Offset = (ulong)this._deadLetters.Count(d => d.Subscription == subscription);
            this._deadLetters.Add((subscription, deadLetter));
            return Task.FromResult(deadLetter.Offset);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<DeadLetter> ReadAsync(string subscription, StreamReadDirection readDirection, long offset, ulong length, CancellationToken cancellationToken = default)
    {
        List<DeadLetter> deadLetters;
        lock (this._lock) deadLetters = [.. this._deadLetters.Where(d => d.Subscription == subscription).Select(d => d.DeadLetter)];
        IEnumerable<DeadLetter> results = readDirection == StreamReadDirection.Forwards
            ? deadLetters.Where(d => offset < 0 || d.Offset >= (ulong)offset)
            : deadLetters.Where(d => offset < 0 || d.Offset <= (ulong)offset).Reverse();
        return results.Take((int)length).ToAsyncEnumerable();
    }

}
