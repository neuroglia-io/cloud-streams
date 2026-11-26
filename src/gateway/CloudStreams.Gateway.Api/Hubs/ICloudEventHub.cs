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

using Microsoft.AspNetCore.SignalR;

namespace CloudStreams.Gateway.Api.Hubs;

/// <summary>
/// Defines the fundamentals of a <see cref="Hub"/> used to observe <see cref="CloudEvent"/>s
/// </summary>
public interface ICloudEventHub
{

    /// <summary>
    /// Only streams the cloud events of the specified source and type to the calling client, rather than all of them (see #221)
    /// </summary>
    /// <param name="source">The source of the cloud events to stream, if any. If not set, the cloud events of any source are streamed</param>
    /// <param name="type">The type of the cloud events to stream, if any. If not set, the cloud events of any type are streamed</param>
    /// <param name="namespace">The namespace of the cloud events to stream, if any. If not set, the cloud events of every namespace are streamed, which requires access to the whole cluster</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    Task Subscribe(string? source, string? type, string? @namespace = null);

}
