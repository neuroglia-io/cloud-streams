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

using CloudStreams.Core.Api.Client.Services;
using CloudStreams.Core.Api.Controllers;
using CloudStreams.Core.Application.Commands.Resources.Generic;
using CloudStreams.Core.Application.Queries.Partitions;
using CloudStreams.Core.Application.Queries.Resources.Generic;
using CloudStreams.Core.Application.Queries.Streams;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Neuroglia.Mediation;
using Neuroglia.Serialization;
using BrokerResource = CloudStreams.Core.Resources.Broker;
using GatewayResource = CloudStreams.Core.Resources.Gateway;
using System.Collections.Concurrent;

namespace CloudStreams.UnitTests.Cases.Core;

/// <summary>
/// Drives the real Core API controllers with the real API client, to verify that each client call reaches the action it is meant for, with its parameters bound as
/// sent. The client and the server are maintained separately, and have drifted apart before (see #178 and #204)
/// </summary>
public class CoreApiClientContractTests
    : IAsyncLifetime
{

    WebApplication _app = null!;
    readonly RecordingMediator _mediator = new();
    CloudStreamsCoreApiClient _client = null!;

    static Subscription Sample(string name = "orders") => new()
    {
        Metadata = new() { Name = name },
        Spec = new() { Subscriber = new() { Uri = new("https://subscriber.example.com/events") } }
    };

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IMediator>(this._mediator);
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(SubscriptionsController).Assembly)
            .AddJsonOptions(options => Neuroglia.Serialization.Json.JsonSerializer.DefaultOptionsConfiguration(options.JsonSerializerOptions));
        this._app = builder.Build();
        this._app.MapControllers();
        await this._app.StartAsync();
        var httpClient = this._app.GetTestClient();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IJsonSerializer>(Neuroglia.Serialization.Json.JsonSerializer.Default)
            .BuildServiceProvider();
        this._client = new CloudStreamsCoreApiClient(services, NullLoggerFactory.Instance, Neuroglia.Serialization.Json.JsonSerializer.Default, httpClient);
    }

    public async Task DisposeAsync() => await this._app.DisposeAsync();

    TRequest Single<TRequest>() => Assert.IsType<TRequest>(Assert.Single(this._mediator.Requests));

    // ---------- cloud events ----------

    [Fact]
    public async Task Listing_Partitions_Should_Bind_Every_Parameter()
    {
        // act
        await this._client.CloudEvents.Partitions.ListPartitionsByTypeAsync(CloudEventPartitionType.BySource, "https://orders.example.com/a?b=c&d", 25, "next&token");

        // assert
        var query = this.Single<ListEventPartitionIdsQuery>();
        Assert.Equal(CloudEventPartitionType.BySource, query.PartitionType);
        Assert.Equal("https://orders.example.com/a?b=c&d", query.Prefix);
        Assert.Equal(25, query.MaxResults);
        Assert.Equal("next&token", query.ContinuationToken);
    }

    [Fact]
    public async Task Getting_Partition_Metadata_Should_Bind_The_Partition()
    {
        // act
        await this._client.CloudEvents.Partitions.GetPartitionMetadataAsync(CloudEventPartitionType.ByCorrelationId, "a/b?c=d");

        // assert
        var query = this.Single<GetEventPartitionMetadataQuery>();
        Assert.Equal(CloudEventPartitionType.ByCorrelationId, query.Partition.Type);
        Assert.Equal("a/b?c=d", query.Partition.Id);
    }

    [Fact]
    public async Task Getting_Stream_Metadata_Should_Reach_Its_Action()
    {
        // act
        await this._client.CloudEvents.Stream.GetStreamMetadataAsync();

        // assert
        this.Single<GetEventStreamMetadataQuery>();
    }

    [Fact]
    public async Task Reading_A_Partition_Should_Bind_Every_Option()
    {
        // arrange
        var options = new StreamReadOptions(new(CloudEventPartitionType.BySource, "https://orders.example.com/?a=b&c"), StreamReadDirection.Backwards, 42, 7);

        // act
        await (await this._client.CloudEvents.Stream.ReadStreamAsync(options)).ToListAsync();

        // assert
        var query = this.Single<ReadEventStreamQuery>();
        Assert.Equal(StreamReadDirection.Backwards, query.Options.Direction);
        Assert.Equal(42, query.Options.Offset);
        Assert.Equal(7ul, query.Options.Length);
        Assert.Equal(CloudEventPartitionType.BySource, query.Options.Partition!.Type);
        Assert.Equal("https://orders.example.com/?a=b&c", query.Options.Partition.Id);
    }

    [Fact]
    public async Task Reading_The_Whole_Stream_Should_Bind_No_Partition()
    {
        // act
        await (await this._client.CloudEvents.Stream.ReadStreamAsync()).ToListAsync();

        // assert
        var query = this.Single<ReadEventStreamQuery>();
        Assert.Null(query.Options.Partition);
        Assert.Null(query.Options.Offset);
        Assert.Equal(StreamReadDirection.Forwards, query.Options.Direction);
    }

    // ---------- resources ----------

    [Fact]
    public async Task Creating_A_Resource_Should_Send_It_Whole()
    {
        // act
        await this._client.Resources.Subscriptions.CreateAsync(Sample("created"));

        // assert
        var command = this.Single<CreateResourceCommand<Subscription>>();
        Assert.Equal("created", command.Resource.GetName());
        Assert.Equal(new Uri("https://subscriber.example.com/events"), command.Resource.Spec.Subscriber.Uri);
        Assert.False(command.DryRun);
    }

    [Fact]
    public async Task Replacing_A_Resource_Should_Reach_Its_Action()
    {
        // act
        await this._client.Resources.Subscriptions.UpdateAsync(Sample("replaced"));

        // assert
        Assert.Equal("replaced", this.Single<ReplaceResourceCommand<Subscription>>().Resource.GetName());
    }

    [Fact]
    public async Task Getting_A_Resource_Should_Bind_Its_Name()
    {
        // act
        await this._client.Resources.Brokers.GetAsync("broker 1");

        // assert
        Assert.Equal("broker 1", this.Single<GetResourceQuery<BrokerResource>>().Name);
    }

    [Fact]
    public async Task Listing_Resources_Should_Bind_Label_Selectors()
    {
        // act
        await (await this._client.Resources.Gateways.ListAsync(labelSelectors: [new LabelSelector("tier", LabelSelectionOperator.Equals, "edge")])).ToListAsync();

        // assert
        var selector = Assert.Single(this.Single<GetResourcesQuery<GatewayResource>>().LabelSelectors!);
        Assert.Equal("tier", selector.Key);
        Assert.Equal("edge", selector.Value);
    }

    [Fact]
    public async Task Getting_A_Resource_Definition_Should_Reach_Its_Action()
    {
        // act
        await this._client.Resources.Subscriptions.GetDefinitionAsync();

        // assert
        this.Single<GetResourceDefinitionQuery<Subscription>>();
    }

    [Fact]
    public async Task Patching_A_Resource_Should_Bind_Its_Name_And_Patch()
    {
        // act
        await this._client.Resources.Subscriptions.PatchAsync(new Patch(PatchType.JsonMergePatch, new { spec = new { subscriber = new { rateLimit = 5 } } }), "orders");

        // assert
        var command = this.Single<PatchResourceCommand<Subscription>>();
        Assert.Equal("orders", command.Name);
        Assert.Equal(PatchType.JsonMergePatch, command.Patch.Type);
        Assert.Contains("rateLimit", Neuroglia.Serialization.Json.JsonSerializer.Default.SerializeToText(command.Patch.Document));
    }

    [Fact]
    public async Task Patching_A_Resource_Status_Should_Reach_The_Status_Action()
    {
        // act
        await this._client.Resources.Subscriptions.PatchStatusAsync(new Patch(PatchType.JsonMergePatch, new { status = new { phase = "active" } }), "orders");

        // assert
        Assert.Equal("orders", this.Single<PatchResourceStatusCommand<Subscription>>().Name);
    }

    [Fact]
    public async Task Deleting_A_Resource_Should_Bind_Its_Name()
    {
        // act
        await this._client.Resources.Subscriptions.DeleteAsync("orders");

        // assert
        Assert.Equal("orders", this.Single<DeleteResourceCommand<Subscription>>().Name);
    }

    [Fact]
    public async Task A_Problem_Returned_By_The_Server_Should_Be_Thrown_With_Its_Status()
    {
        // arrange
        this._mediator.Status = (int)HttpStatusCode.NotFound;

        // act
        var exception = await Assert.ThrowsAsync<ProblemDetailsException>(() => this._client.Resources.Subscriptions.GetAsync("missing"));

        // assert
        Assert.Equal((int)HttpStatusCode.NotFound, exception.Problem.Status);
    }

    /// <summary>
    /// Represents an <see cref="IMediator"/> that records the requests it executes, and answers each of them with a plausible value of the type of result it declares
    /// </summary>
    sealed class RecordingMediator
        : IMediator
    {

        readonly ConcurrentQueue<object> _requests = new();

        public IReadOnlyCollection<object> Requests => [.. this._requests];

        public int Status { get; set; } = (int)HttpStatusCode.OK;

        public Task<TResult> ExecuteAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
            where TResult : IOperationResult
        {
            this._requests.Enqueue(request);
            var dataType = typeof(TResult).GetGenericType(typeof(IOperationResult<>))?.GetGenericArguments()[0];
            var resultType = dataType == null ? typeof(OperationResult) : typeof(OperationResult<>).MakeGenericType(dataType);
            if (this.Status >= 400)
            {
                var error = new Error(ProblemTypes.NotFound, "Not Found", this.Status, "The resource does not exist");
                return Task.FromResult((TResult)Activator.CreateInstance(resultType, this.Status, null, new[] { error })!);
            }
            var data = dataType == null ? null : CreateData(dataType);
            return Task.FromResult((TResult)Activator.CreateInstance(resultType, this.Status, data, Array.Empty<Error>())!);
        }

        public Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        static object? CreateData(Type type)
        {
            if (type.IsAssignableFrom(typeof(Subscription))) return Sample();
            if (typeof(IResource).IsAssignableFrom(type) && !type.IsAbstract) return Activator.CreateInstance(type);
            var itemType = type.GetGenericType(typeof(IAsyncEnumerable<>))?.GetGenericArguments()[0];
            if (itemType != null) return typeof(AsyncEnumerable).GetMethod(nameof(AsyncEnumerable.Empty))!.MakeGenericMethod(itemType).Invoke(null, null);
            if (type.IsAssignableFrom(typeof(SubscriptionResourceDefinition))) return new SubscriptionResourceDefinition();
            if (type == typeof(PartitionMetadata)) return new PartitionMetadata() { Type = CloudEventPartitionType.ByCorrelationId, Id = "a/b?c=d" };
            return type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null ? null : Activator.CreateInstance(type);
        }

    }

}
