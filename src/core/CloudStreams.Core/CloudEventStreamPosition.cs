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

namespace CloudStreams.Core;

/// <summary>
/// Provides mechanisms to handle positioning/offsetting in cloud event streams
/// </summary>
public static class StreamPosition
{

    /// <summary>
    /// Specifies the start of the stream
    /// </summary>
    public const long StartOfStream = 0;

    /// <summary>
    /// Specifies the end of the stream
    /// </summary>
    public const long EndOfStream = -1;

}
