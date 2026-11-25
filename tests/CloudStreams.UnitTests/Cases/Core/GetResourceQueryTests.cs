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

using CloudStreams.Core.Application.Queries.Resources.Generic;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Covers getting a single resource, which the dashboard's import relies on to tell whether a resource exists (see #227)
/// </summary>
public class GetResourceQueryTests
{

    [Fact]
    public async Task Getting_A_Missing_Resource_Should_Fail_With_Not_Found()
    {
        // arrange
        var repository = Substitute.For<IResourceRepository>();
        repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IResource?>(null));

        // act
        var result = await new GetResourceQueryHandler<Subscription>(repository).HandleAsync(new GetResourceQuery<Subscription>("missing", null), CancellationToken.None);

        // assert
        Assert.Equal(404, result.Status);
        Assert.Contains("missing", Assert.Single(result.Errors!).Detail);
    }

    [Fact]
    public async Task Getting_An_Existing_Resource_Should_Return_It()
    {
        // arrange
        var subscription = new Subscription() { Metadata = new() { Name = "orders" } };
        var repository = Substitute.For<IResourceRepository>();
        repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IResource?>(subscription));

        // act
        var result = await new GetResourceQueryHandler<Subscription>(repository).HandleAsync(new GetResourceQuery<Subscription>("orders", null), CancellationToken.None);

        // assert
        Assert.Equal(200, result.Status);
        Assert.Equal("orders", result.Data!.GetName());
    }

}
