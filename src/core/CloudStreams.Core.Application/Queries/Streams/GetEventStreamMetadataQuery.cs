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

namespace CloudStreams.Core.Application.Queries.Streams;

/// <summary>
/// Represents the <see cref="IQuery{TResult}"/> used to get the metadata of the application's cloud event stream
/// </summary>
public class GetEventStreamMetadataQuery
    : Query<StreamMetadata>
{



}

/// <summary>
/// Represents the service used to handle <see cref="GetEventStreamMetadataQuery"/> instances
/// </summary>
public class GetEventPartitionMetadataQueryHandler(ICloudEventStore eventStore)
    : IQueryHandler<GetEventStreamMetadataQuery, StreamMetadata>
{

    /// <inheritdoc/>
    public virtual async Task<IOperationResult<StreamMetadata>> HandleAsync(GetEventStreamMetadataQuery query, CancellationToken cancellationToken)
    {
        return this.Ok(await eventStore.GetStreamMetadataAsync(cancellationToken));
    }

}
