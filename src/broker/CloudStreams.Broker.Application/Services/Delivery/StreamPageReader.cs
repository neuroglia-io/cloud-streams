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

using System.Runtime.CompilerServices;

namespace CloudStreams.Broker.Application.Services.Delivery;

/// <summary>
/// Reads the records of a stream a page at a time, reading the next page while the current one is being consumed (see #162 and #163)
/// </summary>
/// <param name="readPage">A function that reads a page of records, given the offset of its first record and its maximum length. It returns fewer records than requested at the end of the stream</param>
/// <param name="pageSize">The maximum amount of records per page</param>
public sealed class StreamPageReader(Func<long, int, CancellationToken, Task<List<CloudEventRecord>>> readPage, int pageSize)
{

    /// <summary>
    /// Gets the delay after which to read again a record that has been appended, but is not readable yet
    /// </summary>
    /// <remarks>A partition's records are linked by a projection shortly after being stored</remarks>
    public static readonly TimeSpan UnreadableRecordDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets the maximum amount of records per page
    /// </summary>
    public int PageSize { get; } = pageSize > 0 ? pageSize : throw new ArgumentOutOfRangeException(nameof(pageSize));

    /// <summary>
    /// Reads the pages of the stream, starting at the specified offset, until the specified length is reached
    /// </summary>
    /// <param name="offset">The offset of the first record to read</param>
    /// <param name="length">A function that gets the current length of the stream, which is evaluated before reading each page</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IAsyncEnumerable{T}"/> containing the pages read, none of which is empty</returns>
    /// <remarks>
    /// The page that follows a full page is read while the full page is consumed. If the consumer stops early, the page read ahead is abandoned, but still awaited,
    /// so that its failure, if any, is observed
    /// </remarks>
    public async IAsyncEnumerable<IReadOnlyList<CloudEventRecord>> ReadPagesAsync(long offset, Func<ulong> length, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(length);
        using var prefetchCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<List<CloudEventRecord>>? prefetch = null;
        var prefetchOffset = 0L;
        try
        {
            while (!cancellationToken.IsCancellationRequested && (ulong)offset < length())
            {
                var page = prefetch != null && prefetchOffset == offset
                    ? await prefetch.ConfigureAwait(false)
                    : await readPage(offset, this.PageSize, cancellationToken).ConfigureAwait(false);
                prefetch = null;
                if (page.Count == 0)
                {
                    await Task.Delay(UnreadableRecordDelay, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (page.Count == this.PageSize)
                {
                    prefetchOffset = (long)page[^1].Sequence + 1;
                    prefetch = readPage(prefetchOffset, this.PageSize, prefetchCancellationTokenSource.Token);
                }
                // the next page follows the last record read rather than the amount of records read: the records before the requested offset may have expired, in which case the page starts further (see #206)
                offset = (long)page[^1].Sequence + 1;
                yield return page;
            }
        }
        finally
        {
            prefetchCancellationTokenSource.Cancel();
            if (prefetch != null)
            {
                try { await prefetch.ConfigureAwait(false); }
                catch { }
            }
        }
    }

}
