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

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CloudStreams.UnitTests;

/// <summary>
/// Performs the process-wide setup the Cloud Streams components assume a host has already done
/// </summary>
internal static class TestSetup
{

    /// <summary>
    /// Assigns the shared <see cref="ActivitySource"/>. Production code assigns it in <c>Program.cs</c> and dereferences it unguarded, so tests must do the same or every call sites throws
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize() => CloudStreamsDefaults.Telemetry.ActivitySource ??= new("Cloud Streams Tests", "0.0.0");

}
