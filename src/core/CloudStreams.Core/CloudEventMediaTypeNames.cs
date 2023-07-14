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
/// Enumerates all supported cloud event media types names
/// </summary>
public static class CloudEventMediaTypeNames
{

    /// <summary>
    /// Gets the 'application/cloudevents' media type, which assumes an encoding in JSON
    /// </summary>
    public const string CloudEvents = "application/cloudevents";
    /// <summary>
    /// Gets the 'application/cloudevents+json' media type
    /// </summary>
    public const string CloudEventsJson = CloudEvents + "+json";
    /// <summary>
    /// Gets the 'application/cloudevents+yaml' media type
    /// </summary>
    public const string CloudEventsYaml = CloudEvents + "+yaml";

}
