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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace CloudStreams.IntegrationTests;

/// <summary>
/// Represents a subscriber that records the events delivered to it. Each test registers its own endpoint, under its own path, and decides how to respond
/// </summary>
public sealed class TestSubscriber
    : IAsyncDisposable
{

    readonly ConcurrentDictionary<string, Endpoint> _endpoints = new();
    WebApplication? _app;

    /// <summary>
    /// Gets the base address of the subscriber
    /// </summary>
    public Uri BaseUri { get; private set; } = null!;

    /// <summary>
    /// Starts the subscriber
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        this._app = builder.Build();
        this._app.MapPost("/{name}", async (string name, HttpContext context) =>
        {
            if (!this._endpoints.TryGetValue(name, out var endpoint)) return Results.NotFound();
            using var document = await JsonDocument.ParseAsync(context.Request.Body);
            var offset = context.Request.Headers["cloudstreams-offset"].ToString();
            var ackToken = context.Request.Headers["cloudstreams-ack-token"].ToString();
            // a batch is recorded as one delivery per cloud event, and refused if any of them is (see #212)
            var batch = document.RootElement.ValueKind == JsonValueKind.Array;
            var deliveries = batch
                ? [.. document.RootElement.EnumerateArray().Select(e => new Delivery(e.GetProperty("id").GetString()!, offset, ackToken, e.Clone(), true))]
                : new[] { new Delivery(document.RootElement.GetProperty("id").GetString()!, offset, ackToken, document.RootElement.Clone()) };
            var statuses = deliveries.Select(endpoint.Respond).ToList();
            var status = statuses.FirstOrDefault(s => s is < 200 or >= 300, statuses[^1]);
            foreach (var delivery in deliveries) endpoint.Record(delivery);
            return Results.StatusCode(status);
        });
        await this._app.StartAsync();
        var address = this._app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        this.BaseUri = new(address.TrimEnd('/') + "/");
    }

    /// <summary>
    /// Registers a new endpoint
    /// </summary>
    /// <param name="name">The name of the endpoint, which is also its path</param>
    /// <param name="respond">A function that returns the status code to respond to the specified delivery with. Defaults to 200</param>
    /// <returns>The new endpoint</returns>
    public Endpoint Register(string name, Func<Delivery, int>? respond = null)
    {
        var endpoint = new Endpoint(new(this.BaseUri, name), respond ?? (_ => 200));
        if (!this._endpoints.TryAdd(name, endpoint)) throw new InvalidOperationException($"An endpoint named '{name}' already exists");
        return endpoint;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this._app != null) await this._app.DisposeAsync();
    }

    /// <summary>
    /// Represents an event delivered to the subscriber
    /// </summary>
    /// <param name="Id">The id of the delivered event</param>
    /// <param name="Offset">The offset of the event in the subscription's stream, if any</param>
    /// <param name="AckToken">The token to acknowledge the event with, if any</param>
    /// <param name="Event">The delivered event</param>
    /// <param name="Batched">A boolean indicating whether or not the event has been delivered as part of a batch, whose offset is the one of its first event</param>
    public record Delivery(string Id, string Offset, string AckToken, JsonElement Event, bool Batched = false);

    /// <summary>
    /// Represents an endpoint of the subscriber
    /// </summary>
    /// <param name="uri">The uri of the endpoint</param>
    /// <param name="respond">A function that returns the status code to respond to the specified delivery with</param>
    public sealed class Endpoint(Uri uri, Func<Delivery, int> respond)
    {

        readonly List<Delivery> _deliveries = [];
        readonly SemaphoreSlim _received = new(0);

        /// <summary>
        /// Gets the uri of the endpoint
        /// </summary>
        public Uri Uri { get; } = uri;

        /// <summary>
        /// Gets the deliveries received so far, in order
        /// </summary>
        public IReadOnlyList<Delivery> Deliveries { get { lock (this._deliveries) return [.. this._deliveries]; } }

        internal void Record(Delivery delivery)
        {
            lock (this._deliveries) this._deliveries.Add(delivery);
            this._received.Release();
        }

        internal int Respond(Delivery delivery) => respond(delivery);

        /// <summary>
        /// Waits until the endpoint has received at least the specified amount of deliveries
        /// </summary>
        /// <param name="count">The amount of deliveries to wait for</param>
        /// <param name="timeout">The maximum time to wait for. Defaults to 30 seconds</param>
        /// <returns>The deliveries received so far</returns>
        public async Task<IReadOnlyList<Delivery>> WaitForAsync(int count, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
            while (this.Deliveries.Count < count)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero || !await this._received.WaitAsync(remaining)) throw new TimeoutException($"Received {this.Deliveries.Count} of the {count} expected deliveries: [{string.Join(", ", this.Deliveries.Select(d => d.Id))}]");
            }
            return this.Deliveries;
        }

    }

}
