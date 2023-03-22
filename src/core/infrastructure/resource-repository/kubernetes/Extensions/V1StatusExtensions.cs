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
using k8s.Models;

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="V1Status"/>es
/// </summary>
public static class V1StatusExtensions
{

    /// <summary>
    /// Converts the <see cref="V1Status"/> into a new <see cref="Response"/>
    /// </summary>
    /// <param name="status">The <see cref="V1Status"/> to convert</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response ToResponse(this V1Status status)
    {
        return new()
        {
            Status = status.Code!.Value,
            Title = status.Status,
            Detail = status.Message
        };
    }

    /// <summary>
    /// Converts the <see cref="V1Status"/> into a new <see cref="Response"/>
    /// </summary>
    /// <param name="status">The <see cref="V1Status"/> to convert</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static TResponse ToResponse<TResponse>(this V1Status status)
        where TResponse : Response, new()
    {
        return new()
        {
            Status = status.Code!.Value,
            Title = status.Status,
            Detail = status.Message
        };
    }

}
