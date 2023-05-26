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

using System.Net;

namespace CloudStreams.Core.Application;

/// <summary>
/// Defines extensions for <see cref="ApiResponse"/>s
/// </summary>
public static class ApiResponseExtensions
{

    /// <summary>
    /// Creates a new <see cref="ApiResponse"/> to inform about a validation failure
    /// </summary>
    /// <param name="detail">Describes the validation error</param>
    /// <returns>A new <see cref="ApiResponse"/></returns>
    public static ApiResponse ValidationFailed(string? detail = null) => new(Data.Properties.ProblemTypes.ValidationFailed, Data.Properties.ProblemTitles.ValidationFailed, (int)HttpStatusCode.BadRequest, detail);

    /// <summary>
    /// Creates a new <see cref="ApiResponse"/> to inform about a validation failure
    /// </summary>
    /// <param name="evaluationResults">An object that represents the validation results</param>
    /// <returns>A new <see cref="ApiResponse"/></returns>
    public static ApiResponse ValidationFailed(EvaluationResults evaluationResults)
    {
        return new(Data.Properties.ProblemTypes.ValidationFailed, Data.Properties.ProblemTitles.ValidationFailed, (int)HttpStatusCode.BadRequest)
        {
            Errors = evaluationResults == null ? null : new(evaluationResults.ToErrorList()!)
        };
    }

    /// <summary>
    /// Converts the <see cref="ApiResponse"/> to a new <see cref="ApiResponse{TContent}"/>
    /// </summary>
    /// <typeparam name="TContent">The type of content wrapped by the <see cref="ApiResponse"/></typeparam>
    /// <param name="response">The <see cref="ApiResponse"/> to convert</param>
    /// <returns>A new <see cref="ApiResponse{TContent}"/></returns>
    public static ApiResponse<TContent> OfType<TContent>(this ApiResponse response)
    {
        if (response == null) throw new ArgumentNullException(nameof(response));
        return new(response.Type!, response.Title!, response.Status, response.Detail, response.Instance, response.Errors?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), response.ExtensionData);
    }

}
