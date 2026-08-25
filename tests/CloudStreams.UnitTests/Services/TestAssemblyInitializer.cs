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

namespace CloudStreams.UnitTests.Services;

/// <summary>
/// Performs the process-wide setup the Cloud Streams services perform at startup
/// </summary>
internal static class TestAssemblyInitializer
{

    /// <summary>
    /// Assigns the application's <see cref="ActivitySource"/>.
    /// <para>
    /// Every instrumented method opens an activity against <c>CloudStreamsDefaults.Telemetry.ActivitySource</c>, which is
    /// declared <c>null!</c> and assigned by each host's <c>Program.cs</c> before anything else runs. It is an implicit
    /// global dependency rather than an injected one, so the test host has to satisfy it the same way.
    /// </para>
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize() => CloudStreamsDefaults.Telemetry.ActivitySource ??= new("Cloud Streams Tests");

}
