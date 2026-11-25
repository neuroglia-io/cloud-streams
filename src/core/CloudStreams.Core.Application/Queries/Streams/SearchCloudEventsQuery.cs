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
using Neuroglia.Data.Expressions;
using Neuroglia.Data.Expressions.Services;
using Neuroglia.Data.Infrastructure.EventSourcing;
using System.Text.RegularExpressions;

namespace CloudStreams.Core.Application.Queries.Streams;

/// <summary>
/// Represents the query used to search the cloud event stream, or a partition (see #223)
/// </summary>
/// <param name="options">The options of the search</param>
public class SearchCloudEventsQuery(CloudEventSearchOptions options)
    : Query<CloudEventSearchResult>
{

    /// <summary>
    /// Gets the options of the search
    /// </summary>
    public CloudEventSearchOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

}

/// <summary>
/// Represents the service used to handle <see cref="SearchCloudEventsQuery"/> instances
/// </summary>
/// <param name="eventStore">The service used to read the cloud event stream</param>
/// <param name="expressionEvaluator">The service used to evaluate the search's runtime expression</param>
/// <remarks>
/// Records are read a page at a time, and matched against each criterion: the id exactly, attributes as regular expressions, and the runtime expression against the
/// record, as subscription filters do. Nothing is indexed: the time range, or a partition, bounds what is read, and each search reads at most
/// <see cref="CloudEventSearchOptions.MaxScannedLimit"/> records
/// </remarks>
public class SearchCloudEventsQueryHandler(ICloudEventStore eventStore, IExpressionEvaluator expressionEvaluator)
    : IQueryHandler<SearchCloudEventsQuery, CloudEventSearchResult>
{

    const int PageSize = 100;

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<CloudEventSearchResult>> HandleAsync(SearchCloudEventsQuery query, CancellationToken cancellationToken)
    {
        var options = query.Options;
        if (options.PartitionType.HasValue != !string.IsNullOrWhiteSpace(options.PartitionId)) return Invalid("'partitionType' and 'partitionId' must be set together");
        var maxResults = options.MaxResults ?? 20;
        if (maxResults is < 1 or > CloudEventSearchOptions.MaxResultsLimit) return Invalid($"'maxResults' must be between 1 and {CloudEventSearchOptions.MaxResultsLimit}");
        var maxScanned = options.MaxScanned ?? CloudEventSearchOptions.MaxScannedLimit;
        if (maxScanned is < 1 or > CloudEventSearchOptions.MaxScannedLimit) return Invalid($"'maxScanned' must be between 1 and {CloudEventSearchOptions.MaxScannedLimit}");
        var attributes = new List<(string Name, string Pattern)>();
        foreach (var (name, pattern) in options.Attributes ?? [])
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(pattern)) continue;
            try { _ = new Regex(pattern); }
            catch (ArgumentException ex) { return Invalid($"The pattern of the attribute '{name}' is not a valid regular expression: {ex.Message}"); }
            attributes.Add((name, pattern));
        }
        var expression = string.IsNullOrWhiteSpace(options.Expression) ? null : options.Expression.IsRuntimeExpression() ? options.Expression : $"${{ {options.Expression} }}";
        var partition = options.PartitionType.HasValue ? new PartitionReference(options.PartitionType.Value, options.PartitionId!) : null;
        var result = new CloudEventSearchResult();
        ulong offset;
        if (options.Offset.HasValue) offset = options.Offset.Value;
        else if (options.From.HasValue)
        {
            var found = await eventStore.FindOffsetAsync(options.From.Value, partition, cancellationToken).ConfigureAwait(false);
            if (!found.HasValue) return this.Ok(result);
            offset = found.Value;
        }
        else offset = 0;
        while (true)
        {
            var records = partition == null
                ? eventStore.ReadAsync(StreamReadDirection.Forwards, (long)offset, PageSize, cancellationToken)
                : eventStore.ReadPartitionAsync(partition, StreamReadDirection.Forwards, (long)offset, PageSize, cancellationToken);
            var read = 0;
            await foreach (var record in records.ConfigureAwait(false))
            {
                read++;
                offset = record.Sequence + 1;
                if (options.To.HasValue && record.Timestamp > options.To) return this.Ok(result);
                result.Scanned++;
                if (await this.MatchesAsync(record, options.Id, attributes, expression, cancellationToken).ConfigureAwait(false)) result.Records.Add(record);
                if (result.Records.Count >= maxResults || result.Scanned >= maxScanned)
                {
                    result.NextOffset = offset;
                    return this.Ok(result);
                }
            }
            // a page shorter than requested is the end of the stream
            if (read < PageSize) return this.Ok(result);
        }
    }

    /// <summary>
    /// Determines whether the specified record matches the specified criteria
    /// </summary>
    protected virtual async Task<bool> MatchesAsync(CloudEventRecord record, string? id, IReadOnlyList<(string Name, string Pattern)> attributes, string? expression, CancellationToken cancellationToken)
    {
        var contextAttributes = record.Metadata.ContextAttributes;
        if (!string.IsNullOrWhiteSpace(id) && (!contextAttributes.TryGetValue(CloudEventAttributes.Id, out var value) || value?.ToString() != id)) return false;
        foreach (var (name, pattern) in attributes)
        {
            if (!contextAttributes.TryGetValue(name, out var attribute) || attribute?.ToString() is not { Length: > 0 } text || !CloudStreamsRegex.IsMatch(text, pattern)) return false;
        }
        if (expression == null) return true;
        try { return await expressionEvaluator.EvaluateConditionAsync(expression, record, cancellationToken: cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException) { throw new ProblemDetailsException(new(InvalidSearchProblemType, "Bad Request", (int)HttpStatusCode.BadRequest, $"The expression could not be evaluated: {ex.Message}")); }
    }

    /// <summary>
    /// Gets the type of the problem that describes an invalid search
    /// </summary>
    public static readonly Uri InvalidSearchProblemType = new("https://cloud-streams.io/problems/invalid-search");

    static OperationResult<CloudEventSearchResult> Invalid(string detail) => new((int)HttpStatusCode.BadRequest, errors: [new Error(InvalidSearchProblemType, "Bad Request", (int)HttpStatusCode.BadRequest, detail)]);

}
