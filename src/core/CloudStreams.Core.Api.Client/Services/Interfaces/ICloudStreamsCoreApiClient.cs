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

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of a service used to interact with a Cloud Streams gateway's API
/// </summary>
public interface ICloudStreamsCoreApiClient
{

    /// <summary>
    /// Gets the API used to manage cloud events
    /// </summary>
    ICloudEventsApi CloudEvents { get; }

    /// <summary>
    /// Gets the API used to manage resources
    /// </summary>
    IResourceManagementApiClient Resources { get; }

}
