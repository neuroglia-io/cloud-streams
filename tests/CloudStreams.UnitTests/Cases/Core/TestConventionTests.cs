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

using System.Reflection;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Guards the convention the CI workflow's per-domain test filters depend on
/// </summary>
public class TestConventionTests
{

    /// <summary>
    /// The namespace fragments <c>.github/workflows/test-dotnet.yml</c> filters on. A test outside all of them is built,
    /// discovered by no step, and never run — the same silent-skip failure that let the surviving <c>Cases.Core</c> filter
    /// pass against a solution with zero test assemblies for two years. Adding a domain means adding a step there and an
    /// entry here.
    /// </summary>
    static readonly string[] FilteredDomains = ["Cases.Core", "Cases.Broker"];

    [Fact]
    public void Every_Test_Should_Live_Under_A_Domain_The_Ci_Workflow_Runs()
    {
        //arrange
        var tests = typeof(TestConventionTests).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<FactAttribute>().Any() || m.GetCustomAttributes<TheoryAttribute>().Any());

        //act
        var orphans = tests
            .Where(m => !FilteredDomains.Any(d => $"{m.DeclaringType?.FullName}".Contains(d, StringComparison.Ordinal)))
            .Select(m => $"{m.DeclaringType?.FullName}.{m.Name}")
            .Distinct()
            .ToList();

        //assert
        orphans.Should().BeEmpty($"every test must sit under one of [{string.Join(", ", FilteredDomains)}] or no CI step will run it");
    }

    [Fact]
    public void The_Assembly_Should_Contain_Tests_For_Every_Filtered_Domain()
    {
        //arrange
        //a filter that matches nothing is the failure mode this whole suite exists to prevent
        var types = typeof(TestConventionTests).Assembly.GetTypes().Select(t => t.FullName ?? string.Empty).ToList();

        //assert
        foreach (var domain in FilteredDomains) types.Should().Contain(t => t.Contains(domain, StringComparison.Ordinal), $"the CI step filtering on '{domain}' must have something to run");
    }

}
