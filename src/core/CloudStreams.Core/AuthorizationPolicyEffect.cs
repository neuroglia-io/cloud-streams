﻿// Copyright © 2024-Present The Cloud Streams Authors
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
/// Enumerates default authorization policy effects
/// </summary>
public static class AuthorizationPolicyEffect
{

    /// <summary>
    /// Indicates that the policy's effect is to grant authorization when it applies
    /// </summary>
    public const string Authorize = "authorize";
    
    /// <summary>
    /// Indicates that the policy's effect is to forbid authorization when it applies
    /// </summary>
    public const string Forbid = "forbid";

}
