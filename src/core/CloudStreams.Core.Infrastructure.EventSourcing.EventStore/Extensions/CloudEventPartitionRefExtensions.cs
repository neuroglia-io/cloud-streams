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

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="PartitionReference"/>s
/// </summary>
public static class CloudEventPartitionRefExtensions
{

    /// <summary>
    /// Gets the EventStore stream name for the specified partition
    /// </summary>
    /// <param name="partition">A reference of the partition to get the EventStore stream name for</param>
    /// <returns>The EventStore stream name for the specified <see cref="PartitionReference"/></returns>
    public static string GetStreamName(this PartitionReference partition)
    {
        return partition.Type switch
        {
            CloudEventPartitionType.BySource => EventStoreStreams.ByCloudEventSource(new(partition.Id)),
            CloudEventPartitionType.BySubject => EventStoreStreams.ByCloudEventSubject(partition.Id),
            CloudEventPartitionType.ByType => EventStoreStreams.ByCloudEventType(partition.Id),
            CloudEventPartitionType.ByCorrelationId => EventStoreStreams.ByCorrelationId(partition.Id),
            CloudEventPartitionType.ByCausationId => EventStoreStreams.ByCausationId(partition.Id),
            _ => throw new NotSupportedException($"The specified partition type '{partition.Type}' is not supported")
        };
    }

}
