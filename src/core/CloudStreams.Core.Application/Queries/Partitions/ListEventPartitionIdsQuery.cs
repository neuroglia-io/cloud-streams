// Copyright © 2023-Present The Cloud Streams Authors
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

using CloudStreams.Core.Infrastructure;
using CloudStreams.Core.Infrastructure.Services;
using Hylo.Api.Application;
using System.ComponentModel.DataAnnotations;

namespace CloudStreams.Core.Application.Queries.Partitions;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to list the ids of event partitions
/// </summary>
public class ListEventPartitionIdsQuery
    : IQuery<IAsyncEnumerable<string>>
{

    /// <summary>
    /// Initializes a new <see cref="ListEventPartitionIdsQuery"/>
    /// </summary>
    /// <param name="partitionType">The type of partitions to list the ids of</param>
    public ListEventPartitionIdsQuery(CloudEventPartitionType partitionType)
    {
        this.PartitionType = partitionType;
    }

    /// <summary>
    /// Gets the type of partitions to list the ids of
    /// </summary>
    [Required]
    public virtual CloudEventPartitionType PartitionType { get; set; }

}

/// <summary>
/// Represents the service used to handle <see cref="ListEventPartitionIdsQuery"/> instances
/// </summary>
public class ListEventPartitionIdsQueryHandler
    : IQueryHandler<ListEventPartitionIdsQuery, IAsyncEnumerable<string>>
{

    /// <inheritdoc/>
    public ListEventPartitionIdsQueryHandler(IEventStoreProvider eventStoreProvider)
    {
        this._eventStoreProvider = eventStoreProvider;
    }

    IEventStoreProvider _eventStoreProvider;

    Task<ApiResponse<IAsyncEnumerable<string>>> MediatR.IRequestHandler<ListEventPartitionIdsQuery, ApiResponse<IAsyncEnumerable<string>>>.Handle(ListEventPartitionIdsQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(this.Ok(this._eventStoreProvider.GetEventStore().ListPartitionIdsAsync(query.PartitionType, cancellationToken)));
    }

}
