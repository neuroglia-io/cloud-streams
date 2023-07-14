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
/// Defines extensions for <see cref="OperationResult"/>s
/// </summary>
public static class OperationResultExtensions
{

    /// <summary>
    /// Determines whether or not the <see cref="OperationResult"/> defines a success status
    /// </summary>
    /// <param name="result">The <see cref="OperationResult"/> to check</param>
    /// <returns>A boolean indicating whether or not the <see cref="OperationResult"/> defines a success status</returns>
    public static bool IsSuccessStatusCode(this OperationResult result) => result.Status is >= 200 and < 300;

    /// <summary>
    /// Converts the <see cref="OperationResult"/> to a new <see cref="OperationResult{TContent}"/>
    /// </summary>
    /// <typeparam name="TContent">The type of content wrapped by the <see cref="OperationResult"/></typeparam>
    /// <param name="result">The <see cref="OperationResult"/> to convert</param>
    /// <returns>A new <see cref="OperationResult{TContent}"/></returns>
    public static OperationResult<TContent> OfType<TContent>(this OperationResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        return new(result.Type!, result.Title!, result.Status, result.Detail, result.Instance, result.Errors?.ToDictionary(e => e.Key, e => e.Value), result.ExtensionData);
    }

}