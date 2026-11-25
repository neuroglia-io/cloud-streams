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

namespace CloudStreams.Core;

/// <summary>
/// Exposes the HTTP headers the broker adds to the requests it sends to subscribers
/// </summary>
public static class CloudStreamsHeaders
{

    /// <summary>
    /// Gets the name of the header that conveys the name of the subscription the cloud event is delivered for
    /// </summary>
    public const string Subscription = "cloudstreams-subscription";

    /// <summary>
    /// Gets the name of the header that conveys the offset of the delivered cloud event in the subscription's stream
    /// </summary>
    public const string Offset = "cloudstreams-offset";

    /// <summary>
    /// Gets the name of the header that conveys the single-use token the subscriber must present to acknowledge the cloud event, if it defers its acknowledgement
    /// </summary>
    public const string AckToken = "cloudstreams-ack-token";

    /// <summary>
    /// Gets the name of the header that conveys the uri the subscriber must call to acknowledge the cloud event, if it defers its acknowledgement and if the broker knows it
    /// </summary>
    public const string AckUri = "cloudstreams-ack-uri";

    /// <summary>
    /// Gets the name of the header with which the gateway tells publishers what became of a cloud event it accepted: either <c>stored</c>, <c>ignored</c> when
    /// the split-horizon policy dropped it as looping, or <c>duplicate</c> when it had already been stored within the deduplication window
    /// </summary>
    public const string Outcome = "cloudstreams-outcome";

    /// <summary>
    /// Exposes the values of the <see cref="Outcome"/> header
    /// </summary>
    public static class Outcomes
    {

        /// <summary>
        /// Indicates that the cloud event has been stored
        /// </summary>
        public const string Stored = "stored";

        /// <summary>
        /// Indicates that the cloud event has been ignored, because the gateway's split-horizon policy detected a loop
        /// </summary>
        public const string Ignored = "ignored";

        /// <summary>
        /// Indicates that the cloud event has already been stored within the gateway's deduplication window, and has not been stored again
        /// </summary>
        public const string Duplicate = "duplicate";

    }

}
