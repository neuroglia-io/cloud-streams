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

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="long"/>s
/// </summary>
public static class Int64Extensions
{

    /// <summary>
    /// Converts an <see cref="long"/> into a new <see cref="FromStream"/> value
    /// </summary>
    /// <param name="value">The value to convert</param>
    /// <returns>A new <see cref="FromStream"/> value</returns>
    public static FromStream ToSubscriptionPosition(this long value)
    {
        return value switch
        {
            StreamPosition.StartOfStream => FromStream.Start,
            StreamPosition.EndOfStream => FromStream.End,
            _ => FromStream.After(EventStore.Client.StreamPosition.FromInt64(value))
        };
    }

}
