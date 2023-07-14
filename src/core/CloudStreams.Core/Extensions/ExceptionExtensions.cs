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
/// Defines extensions for <see cref="Exception"/>s
/// </summary>
public static class ExceptionExtensions
{

    /// <summary>
    /// Converts the <see cref="Exception"/> into new <see cref="ProblemDetails"/>
    /// </summary>
    /// <param name="exception">The <see cref="Exception"/> to convert</param>
    /// <returns>New <see cref="ProblemDetails"/> that describe the <see cref="Exception"/></returns>
    public static ProblemDetails ToProblemDetails(this Exception exception)
    {
        if (exception == null) throw new ArgumentNullException(nameof(exception));
        if (exception is CloudStreamsException csex && csex.ProblemDetails != null) return csex.ProblemDetails;
        var statusCode = exception switch
        {
            HttpRequestException httpEx => httpEx.StatusCode.HasValue ? (int)httpEx.StatusCode : 500,
            _ => 500
        };
        return new ProblemDetails(new Uri(exception.GetType().FullName!, UriKind.Relative), exception.GetType().Name, statusCode, exception.Message);
    }

}