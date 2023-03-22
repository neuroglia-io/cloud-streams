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

using k8s;

namespace CloudStreams.Core.Infrastructure;


/// <summary>
/// Defines extensions for <see cref="WatchEventType"/>s
/// </summary>
public static class WatchEventTypeExtensions
{

    /// <summary>
    /// Converts the watch event type to its CloudStreams equivalency
    /// </summary>
    /// <param name="type">The watch event type to convert</param>
    /// <returns>The converted watch event type</returns>
    public static ResourceWatchEventType ToCloudStreamsEventType(this WatchEventType type)
    {
        return type switch
        {
            WatchEventType.Added => ResourceWatchEventType.Created,
            WatchEventType.Deleted => ResourceWatchEventType.Deleted,
            WatchEventType.Error => ResourceWatchEventType.Error,
            WatchEventType.Modified => ResourceWatchEventType.Updated,
            _ => throw new NotSupportedException($"The specified {nameof(WatchEventType)} '{type}' is not supported")
        };
    }

}
