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

namespace CloudStreams.Core.Application;

/// <summary>
/// Defines extensions for <see cref="PartitionReference"/>s
/// </summary>
public static class PartitionReferenceExtensions
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
            CloudEventPartitionType.BySource => Streams.ByCloudEventSource(new(partition.Id)),
            CloudEventPartitionType.BySubject => Streams.ByCloudEventSubject(partition.Id),
            CloudEventPartitionType.ByType => Streams.ByCloudEventType(partition.Id),
            CloudEventPartitionType.ByCorrelationId => Streams.ByCorrelationId(partition.Id),
            CloudEventPartitionType.ByCausationId => Streams.ByCausationId(partition.Id),
            _ => throw new NotSupportedException($"The specified partition type '{partition.Type}' is not supported")
        };
    }

}
