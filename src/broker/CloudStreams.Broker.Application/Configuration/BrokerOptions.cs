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

namespace CloudStreams.Broker.Application.Configuration;

/// <summary>
/// Represents the options used to configure a Cloud Streams cloud event gateway
/// </summary>
public class BrokerOptions
{

    /// <summary>
    /// Gets the prefix for all Cloud Streams broker related environment variables
    /// </summary>
    public const string EnvironmentVariablePrefix = "CLOUDSTREAMS_BROKER_";

    /// <summary>
    /// Gets/sets the gateway's name
    /// </summary>
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the gateway's namespace
    /// </summary>
    public virtual string? Namespace { get; set; } = null!;

}
