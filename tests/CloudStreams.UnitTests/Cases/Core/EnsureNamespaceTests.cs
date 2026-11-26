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

using CloudStreams.Core.Application;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers how gateways and brokers create the namespace of the resource they serve, which must exist before the resource can be created (see #215)
/// </summary>
public class EnsureNamespaceTests
{

    static ProblemDetailsException Problem(HttpStatusCode status) => new(new ProblemDetails(new Uri("https://cloud-streams.io/problems/test"), status.ToString(), (int)status));

    [Fact]
    public async Task A_Missing_Namespace_Should_Be_Created()
    {
        // arrange
        var repository = Substitute.For<IResourceRepository>();
        repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns<IResource?>(_ => throw Problem(HttpStatusCode.NotFound));

        // act
        await repository.EnsureNamespaceAsync("tenant-a");

        // assert
        await repository.Received(1).AddAsync(Arg.Is<IResource>(r => r.GetName() == "tenant-a"), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Namespace_Created_By_Another_Process_In_The_Meantime_Should_Be_Accepted()
    {
        // arrange
        var repository = Substitute.For<IResourceRepository>();
        repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns<IResource?>(_ => throw Problem(HttpStatusCode.NotFound));
        repository.AddAsync(Arg.Any<IResource>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns<IResource>(_ => throw Problem(HttpStatusCode.Conflict));

        // act & assert
        await repository.EnsureNamespaceAsync("tenant-a");
    }

    [Fact]
    public async Task An_Invalid_Namespace_Name_Should_Be_Refused()
    {
        var repository = Substitute.For<IResourceRepository>();
        await Assert.ThrowsAsync<ArgumentException>(() => repository.EnsureNamespaceAsync("Tenant_A"));
        await repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default!, default!, default!, default, default);
    }

}
