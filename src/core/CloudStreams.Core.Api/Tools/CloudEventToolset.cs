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

using CloudStreams.Core.Application.Queries.Partitions;
using CloudStreams.Core.Application.Queries.Streams;
using ModelContextProtocol.Server;
using Neuroglia.Data.Infrastructure.EventSourcing;
using Neuroglia.Eventing.CloudEvents;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CloudStreams.Core.Api.Tools;

/// <summary>
/// Represents the MCP toolset used to manage <see cref="CloudEvent"/>s
/// </summary>
/// <param name="logger">The service used to perform logging</param>
/// <param name="mediator">The service used to mediate calls</param>
[McpServerToolType]
public class CloudEventToolset(ILogger<CloudEventToolset> logger, IMediator mediator)
{

    /// <summary>
    /// Gets the service used to perform logging
    /// </summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// Gets the service used to mediate calls
    /// </summary>
    protected IMediator Mediator { get; } = mediator;

    /// <summary>
    /// Gets the cloud event stream's metadata
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The cloud event stream's metadata</returns>
    [McpServerTool(Name = "GetStreamMetadata"), Description("Gets the cloud event stream's metadata")]
    public virtual async Task<StreamMetadata> GetStreamMetadataAsync(CancellationToken cancellationToken = default)
    {
        var result = ProcessResult(await Mediator.ExecuteAsync(new GetEventStreamMetadataQuery(), cancellationToken).ConfigureAwait(false));
        return result.Data!;
    }

    /// <summary>
    /// Gets the cloud event partition's metadata
    /// </summary>
    /// <param name="type">The type of the partition to get the metadata of</param>
    /// <param name="id">The id of the partition to get the metadata of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The cloud event partition's metadata</returns>
    [McpServerTool(Name = "GetPartitionMetadata"), Description("Gets the specified cloud event partition's metadata")]
    public virtual async Task<PartitionMetadata> GetStreamMetadataAsync(
        [Description("The type of the partition to get the metadata of")] CloudEventPartitionType type,
        [Description("The id of the partition to get the metadata of")] string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var result = ProcessResult(await Mediator.ExecuteAsync(new GetEventPartitionMetadataQuery(new(type, id)), cancellationToken).ConfigureAwait(false));
        return result.Data!;
    }

    /// <summary>
    /// Lists the ids of the cloud event partitions of the specified type
    /// </summary>
    /// <param name="type">The type of the partitions to list the ids of</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing the ids of the partitions of the specified type</returns>
    [HttpGet("{type}")]
    [ProducesResponseType(typeof(IEnumerable<string>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public virtual async Task<IEnumerable<string>> ListPartitionsByType(
        [Description("The type of the partitions to list the ids of")]  CloudEventPartitionType type, 
        CancellationToken cancellationToken)
    {
        var result = ProcessResult(await Mediator.ExecuteAsync(new ListEventPartitionIdsQuery(type), cancellationToken).ConfigureAwait(false));
        return await result.Data!.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads events from the specified stream
    /// </summary>
    /// <param name="partitionType">The referenced stream partition's type</param>
    /// <param name="partitionId">The referenced stream partition's id</param>
    /// <param name="direction">The direction in which to read the stream of cloud events</param>
    /// <param name="offset">The offset starting from which to read the stream</param>
    /// <param name="length">The amount of events to read from the stream</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing the events that have been read</returns>
    [McpServerTool(Name = "ReadStream"), Description("Reads events from the specified stream")]
    public virtual async Task<IEnumerable<CloudEventRecord>> ReadStreamAsync(
        [Description("The referenced stream partition's type")] CloudEventPartitionType? partitionType = null,
        [Description("The referenced stream partition's id")] string? partitionId = null,
        [Description("The direction in which to read the stream of cloud events")] StreamReadDirection direction = StreamReadDirection.Forwards,
        [Description("The offset starting from which to read the stream")] long? offset = null,
        [Description("The amount of events to read from the stream")] ulong length = 100,
        CancellationToken cancellationToken = default)
    {
        var partitionReference = partitionType.HasValue && !string.IsNullOrWhiteSpace(partitionId) ? new PartitionReference(partitionType.Value, partitionId) : null;
        var readOptions = new StreamReadOptions(partitionReference!, direction, offset, length, StreamReadOutputFormat.Record);
        var result = ProcessResult(await Mediator.ExecuteAsync(new ReadEventStreamQuery(readOptions), cancellationToken).ConfigureAwait(false));
        return await result.Data!.OfType<CloudEventRecord>().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Processes the specified <see cref="IOperationResult"/>
    /// </summary>
    /// <typeparam name="T">The type of data wrapped by the <see cref="IOperationResult"/> to process</typeparam>
    /// <param name="result">The <see cref="IOperationResult"/> to process</param> 
    /// <param name="callerOperationName">The name of the caller operation</param>
    /// <returns>The processed <see cref="IOperationResult"/></returns>
    protected virtual IOperationResult<T> ProcessResult<T>(IOperationResult<T> result, [CallerMemberName]string? callerOperationName = "Unknown")
    {
        var error = string.Join(Environment.NewLine, result.Errors == null ? "Unknown error" : result.Errors.Select(e => $"[{e.Status}] {e.Title}: {e.Detail}"));
        if (!result.IsSuccess())
        {
            Logger.LogError("An error occurred while executing the operation '{operation}': {error}", callerOperationName, error);
            throw new Exception($"An error occurred while executing the operation '{callerOperationName}': {error}");
        }
        return result;
    }

}
