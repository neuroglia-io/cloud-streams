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

using CloudStreams.Core;

namespace CloudStreams;

/// <summary>
/// Exposes constants about the event sourcing streams used by Cloud Streams
/// </summary>
/// <remarks>
/// Every cloud event is stored in a single stream. The streams of namespaces and of partitions link to its events: those of a partition are named after the
/// partition's namespace and id, so that the partitions of a namespace hold its events only (see #215)
/// </remarks>
public static class StreamNameHelper
{

    /// <summary>
    /// Gets the name of the stream that contains all <see cref="Neuroglia.Eventing.CloudEvents.CloudEvent"/>s
    /// </summary>
    public const string All = "cloud-events";

    /// <summary>
    /// Gets the prefix of the names of the streams that contain the <see cref="Neuroglia.Eventing.CloudEvents.CloudEvent"/>s of a namespace
    /// </summary>
    public const string ByNamespacePrefix = "$by-namespace-";
    /// <summary>
    /// Gets the prefix of the names of all <see cref="CloudEventPartitionType.BySource"/> partition EventStore streams
    /// </summary>
    public const string ByCloudEventSourcePrefix = "$by-source-";
    /// <summary>
    /// Gets the prefix of the names of all <see cref="CloudEventPartitionType.BySubject"/> partition EventStore streams
    /// </summary>
    public const string ByCloudEventSubjectPrefix = "$by-subject-";
    /// <summary>
    /// Gets the prefix of the names of all <see cref="CloudEventPartitionType.ByType"/> partition EventStore streams
    /// </summary>
    public const string ByCloudEventTypePrefix = "$by-type-";
    /// <summary>
    /// Gets the prefix of the names of all <see cref="CloudEventPartitionType.ByCorrelationId"/> partition EventStore streams
    /// </summary>
    public const string ByCorrelationIdPrefix = "$by-correlation-";
    /// <summary>
    /// Gets the prefix of the names of all <see cref="CloudEventPartitionType.ByCausationId"/> partition EventStore streams
    /// </summary>
    public const string ByCausationIdPrefix = "$by-causation-";

    /// <summary>
    /// Gets the separator between the namespace and the id of a partition, in the name of its stream. Namespaces cannot contain it
    /// </summary>
    public const char NamespaceSeparator = '/';

    /// <summary>
    /// Gets the name of the stream that contains the <see cref="Neuroglia.Eventing.CloudEvents.CloudEvent"/>s of the specified namespace
    /// </summary>
    /// <param name="namespace">The namespace to get the stream of</param>
    public static string ByNamespace(string @namespace) => $"{ByNamespacePrefix}{@namespace}";

    /// <summary>
    /// Gets the name of the stream of the specified partition
    /// </summary>
    /// <param name="partitionType">The type of the partition</param>
    /// <param name="namespace">The namespace the partition belongs to</param>
    /// <param name="id">The id of the partition</param>
    /// <returns>The name of the partition's stream</returns>
    public static string ByPartition(CloudEventPartitionType partitionType, string @namespace, string id) => $"{GetPrefix(partitionType)}{@namespace}{NamespaceSeparator}{id}";

    /// <summary>
    /// Gets the prefix of the names of the streams of the partitions of the specified type
    /// </summary>
    /// <param name="partitionType">The type of partitions</param>
    /// <returns>The prefix of the names of their streams</returns>
    public static string GetPrefix(CloudEventPartitionType partitionType) => partitionType switch
    {
        CloudEventPartitionType.BySource => ByCloudEventSourcePrefix,
        CloudEventPartitionType.BySubject => ByCloudEventSubjectPrefix,
        CloudEventPartitionType.ByType => ByCloudEventTypePrefix,
        CloudEventPartitionType.ByCorrelationId => ByCorrelationIdPrefix,
        CloudEventPartitionType.ByCausationId => ByCausationIdPrefix,
        _ => throw new NotSupportedException($"The specified {nameof(CloudEventPartitionType)} '{partitionType}' is not supported")
    };

    /// <summary>
    /// Gets the prefix of the names of the streams that contain the cloud events subscriptions failed to deliver
    /// </summary>
    public const string DeadLettersPrefix = "cloud-streams-dead-letters-";
    /// <summary>
    /// Gets the name of the stream that contains the cloud events the specified subscription failed to deliver
    /// </summary>
    /// <param name="subscription">The qualified name of the subscription</param>
    public static string DeadLetters(string subscription) => $"{DeadLettersPrefix}{subscription}";

    /// <summary>
    /// Determines whether or not the specified EventStore stream name is the one of a partition of the specified type
    /// </summary>
    /// <param name="streamName">The EventStore stream name</param>
    /// <param name="partitionType">The partition type</param>
    /// <returns>A boolean indicating whether or not the specified EventStore stream name is the one of a partition of the specified type</returns>
    public static bool IsPartition(string streamName, CloudEventPartitionType partitionType)
    {
        if (string.IsNullOrWhiteSpace(streamName)) throw new ArgumentNullException(nameof(streamName));
        var prefix = GetPrefix(partitionType);
        return streamName.StartsWith(prefix, StringComparison.Ordinal) && streamName.IndexOf(NamespaceSeparator, prefix.Length) > prefix.Length;
    }

}
