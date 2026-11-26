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
    /// Gets the name of the stream of the specified partition, in the specified namespace
    /// </summary>
    /// <param name="partition">The partition to get the stream of</param>
    /// <param name="namespace">The namespace the partition belongs to. Defaults to the default namespace</param>
    /// <returns>The name of the partition's stream</returns>
    public static string GetStreamName(this PartitionReference partition, string? @namespace)
    {
        ArgumentNullException.ThrowIfNull(partition);
        return StreamNameHelper.ByPartition(partition.Type, string.IsNullOrWhiteSpace(@namespace) ? Namespace.DefaultNamespaceName : @namespace, partition.Id);
    }

}
