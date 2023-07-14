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

using CloudStreams.Core.Data;
using CloudStreams.Core.Infrastructure.Services;
using Hylo.Api.Application;
using System.ComponentModel.DataAnnotations;

namespace CloudStreams.Core.Application.Queries.Partitions;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to get the metadata of the application's cloud event stream
/// </summary>
public class GetEventPartitionMetadataQuery
    : IQuery<PartitionMetadata>
{

    /// <summary>
    /// Initializes a new <see cref="GetEventPartitionMetadataQuery"/>
    /// </summary>
    /// <param name="partition">The cloud event partition to get metadata for</param>
    public GetEventPartitionMetadataQuery(PartitionReference partition)
    {
        this.Partition = partition;
    }

    /// <summary>
    /// Gets the cloud event partition to get metadata for
    /// </summary>
    [Required]
    public virtual PartitionReference Partition { get; protected set; }

}

/// <summary>
/// Represents the service used to handle <see cref="GetEventPartitionMetadataQuery"/> instances
/// </summary>
public class GetEventStreamMetadataQueryHandler
    : IQueryHandler<GetEventPartitionMetadataQuery, PartitionMetadata>
{

    /// <inheritdoc/>
    public GetEventStreamMetadataQueryHandler(IEventStoreProvider eventStoreProvider)
    {
        this._eventStoreProvider = eventStoreProvider;
    }

    IEventStoreProvider _eventStoreProvider;

    async Task<ApiResponse<PartitionMetadata>> MediatR.IRequestHandler<GetEventPartitionMetadataQuery, ApiResponse<PartitionMetadata>>.Handle(GetEventPartitionMetadataQuery query, CancellationToken cancellationToken)
    {
        return this.Ok(await this._eventStoreProvider.GetEventStore().GetPartitionMetadataAsync(query.Partition, cancellationToken));
    }

}
