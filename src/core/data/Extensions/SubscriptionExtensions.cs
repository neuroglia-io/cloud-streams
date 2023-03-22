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

using CloudStreams.Core.Data.Models;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="Subscription"/>s
/// </summary>
public static class SubscriptionExtensions
{

    /// <summary>
    /// Gets the <see cref="Subscription"/>'s offset
    /// </summary>
    /// <param name="subscription">The <see cref="Subscription"/> to get the offset of</param>
    /// <returns>The <see cref="Subscription"/>'s offset</returns>
    public static long GetOffset(this Subscription subscription)
    {
        if (subscription.Status?.Stream?.AckedOffset.HasValue == true && subscription.Status.ObservedGeneration == subscription.Metadata.Generation) return (long)subscription.Status.Stream.AckedOffset.Value;
        if (subscription.Spec.Stream?.Offset.HasValue == true) return subscription.Spec.Stream.Offset.Value;
        return StreamPosition.EndOfStream;
    }

}
