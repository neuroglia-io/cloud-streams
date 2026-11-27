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

namespace CloudStreams.Core.Resources;

/// <summary>
/// Enumerates the statuses of a <see cref="ResourceCondition"/>
/// </summary>
public static class ResourceConditionStatus
{

    /// <summary>
    /// Indicates that the condition holds
    /// </summary>
    public const string True = "True";
    /// <summary>
    /// Indicates that the condition does not hold
    /// </summary>
    public const string False = "False";
    /// <summary>
    /// Indicates that whether the condition holds is unknown
    /// </summary>
    public const string Unknown = "Unknown";

}
