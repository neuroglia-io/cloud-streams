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

namespace CloudStreams;

/// <summary>
/// Represents an <see cref="Exception"/> thrown by a Cloud Streams application
/// </summary>
public class CloudStreamsException
    : Exception
{

    /// <summary>
    /// Initializes a new <see cref="CloudStreamsException"/>
    /// </summary>
    /// <param name="problemDetails">An object used to describe a problem that has occured on the CloudStreams API</param>
    public CloudStreamsException(ProblemDetails? problemDetails = null)
    {
        this.ProblemDetails = problemDetails;
    }

    /// <summary>
    /// Gets an object used to describe a problem that has occured on the CloudStreams API
    /// </summary>
    public ProblemDetails? ProblemDetails { get; }

}