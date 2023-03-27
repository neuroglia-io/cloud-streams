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
/// Defines extensions for <see cref="Response"/>s
/// </summary>
public static class ResponseExtensions
{

    /// <summary>
    /// Determines whether or not the <see cref="Response"/> defines a success status
    /// </summary>
    /// <param name="response">The <see cref="Response"/> to check</param>
    /// <returns>A boolean indicating whether or not the <see cref="Response"/> defines a success status</returns>
    public static bool IsSuccessStatusCode(this Response response) => response.Status is >= 200 and < 300;

    /// <summary>
    /// Converts the <see cref="Response"/> to a new <see cref="Response{TContent}"/>
    /// </summary>
    /// <typeparam name="TContent">The type of content wrapped by the <see cref="Response"/></typeparam>
    /// <param name="response">The <see cref="Response"/> to convert</param>
    /// <returns>A new <see cref="Response{TContent}"/></returns>
    public static Response<TContent> OfType<TContent>(this Response response)
    {
        if(response == null) throw new ArgumentNullException(nameof(response));
        return new(response.Type, response.Title, response.Status, response.Detail, response.Instance, response.Errors, response.ExtensionData);
    }

}