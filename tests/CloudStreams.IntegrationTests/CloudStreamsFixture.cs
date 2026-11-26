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

using CloudStreams.Core;
using CloudStreams.Core.Resources;
using CloudStreams.Core.Api.Client.Services;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Neuroglia.Serialization;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Testcontainers.Kafka;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace CloudStreams.IntegrationTests;

/// <summary>
/// Runs a complete Cloud Streams deployment for the end-to-end tests: EventStoreDB and Redis in containers, and a gateway and a broker started from their build
/// output, as they are in production, plus a subscriber the tests control
/// </summary>
/// <remarks>
/// The gateway and the broker are started as processes rather than hosted in the test process: both are top-level programs, whose entry points would clash
/// </remarks>
public sealed class CloudStreamsFixture
    : IAsyncLifetime
{

    const string EventStoreImage = "eventstore/eventstore:24.10.6";
    const string RedisImage = "redis:7.4-alpine";
    const string KafkaImage = "confluentinc/cp-kafka:7.9.2";
    const string RabbitMqImage = "rabbitmq:4.1-alpine";
    const string NatsImage = "nats:2.11-alpine";
    const string VaultImage = "hashicorp/vault:1.18";

    /// <summary>
    /// Gets the root token of Vault, which runs in development mode
    /// </summary>
    public const string VaultToken = "e2e-root-token";

    /// <summary>
    /// Gets the name of the secret that holds the credentials of RabbitMQ, which the gateway and the broker read from their environment
    /// </summary>
    public const string RabbitMqSecret = "rabbit";
    const string RabbitMqUser = "cloud-streams";
    const string RabbitMqPassword = "e2e-secret";

    readonly RedisContainer _redis = new RedisBuilder(RedisImage).Build();
    readonly IContainer _eventStore = new ContainerBuilder(EventStoreImage)
        .WithEnvironment("EVENTSTORE_INSECURE", "true")
        .WithEnvironment("EVENTSTORE_RUN_PROJECTIONS", "All")
        .WithEnvironment("EVENTSTORE_START_STANDARD_PROJECTIONS", "true")
        .WithPortBinding(2113, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(2113).ForPath("/health/live").ForStatusCode(HttpStatusCode.NoContent)))
        .Build();
    // the transports other than HTTP that cloud events are delivered over and consumed from (see #218)
    readonly KafkaContainer _kafka = new KafkaBuilder(KafkaImage).Build();
    readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder(RabbitMqImage).WithUsername(RabbitMqUser).WithPassword(RabbitMqPassword).Build();
    readonly IContainer _vault = new ContainerBuilder(VaultImage)
        .WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", VaultToken)
        .WithEnvironment("SKIP_SETCAP", "true")
        .WithPortBinding(8200, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8200).ForPath("/v1/sys/health")))
        .Build();
    readonly IContainer _nats = new ContainerBuilder(NatsImage)
        .WithCommand("--jetstream")
        .WithPortBinding(4222, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server is ready"))
        .Build();
    readonly List<ApplicationProcess> _processes = [];

    /// <summary>
    /// Gets the base address of the gateway
    /// </summary>
    public Uri GatewayUri { get; private set; } = null!;

    /// <summary>
    /// Gets an <see cref="HttpClient"/> addressing the gateway
    /// </summary>
    public HttpClient Gateway { get; private set; } = null!;

    /// <summary>
    /// Gets the client of the gateway's API
    /// </summary>
    public ICloudStreamsCoreApiClient Api { get; private set; } = null!;

    /// <summary>
    /// Gets the subscriber the broker delivers events to
    /// </summary>
    public TestSubscriber Subscriber { get; } = new();

    /// <summary>
    /// Gets the address of Kafka's bootstrap server, as <c>host:port</c>
    /// </summary>
    public string KafkaServer { get; private set; } = null!;

    /// <summary>
    /// Gets the address of the NATS server, as <c>host:port</c>
    /// </summary>
    public string NatsServer { get; private set; } = null!;

    /// <summary>
    /// Gets the address of the RabbitMQ server, as <c>host:port</c>
    /// </summary>
    public string RabbitMqServer { get; private set; } = null!;

    /// <summary>
    /// Gets the connection string of RabbitMQ, with its credentials, which the tests use to declare and to consume queues
    /// </summary>
    public string RabbitMqConnectionString => this._rabbitMq.GetConnectionString();

    /// <summary>
    /// Gets the address of Vault, which runs in development mode, with its KV version 2 engine mounted at <c>secret</c>
    /// </summary>
    public Uri VaultUri { get; private set; } = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        // a container that never becomes ready fails the run, rather than hanging it
        using var startup = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await Task.WhenAll(this._redis.StartAsync(startup.Token), this._eventStore.StartAsync(startup.Token), this._kafka.StartAsync(startup.Token), this._rabbitMq.StartAsync(startup.Token), this._nats.StartAsync(startup.Token), this._vault.StartAsync(startup.Token), this.Subscriber.StartAsync());
        this.VaultUri = new($"http://{this._vault.Hostname}:{this._vault.GetMappedPublicPort(8200)}/");
        var bootstrapAddress = this._kafka.GetBootstrapAddress();
        this.KafkaServer = new Uri(bootstrapAddress.Contains("://") ? bootstrapAddress : $"kafka://{bootstrapAddress}").Authority;
        this.NatsServer = $"{this._nats.Hostname}:{this._nats.GetMappedPublicPort(4222)}";
        this.RabbitMqServer = $"{this._rabbitMq.Hostname}:{this._rabbitMq.GetMappedPublicPort(5672)}";
        var environment = new Dictionary<string, string>()
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ConnectionStrings__eventstore"] = $"esdb://{this._eventStore.Hostname}:{this._eventStore.GetMappedPublicPort(2113)}?tls=false",
            ["ConnectionStrings__redis"] = this._redis.GetConnectionString(),
            // secrets are read from the environment, and from Vault (see #218)
            [$"CLOUDSTREAMS_SECRETS__{RabbitMqSecret.ToUpperInvariant()}__USERNAME"] = RabbitMqUser,
            [$"CLOUDSTREAMS_SECRETS__{RabbitMqSecret.ToUpperInvariant()}__PASSWORD"] = RabbitMqPassword,
            ["CloudStreams__Secrets__Providers__0"] = "env",
            ["CloudStreams__Secrets__Providers__1"] = "vault",
            ["CloudStreams__Secrets__Vault__Address"] = this.VaultUri.ToString(),
            ["CloudStreams__Secrets__Vault__Authentication__Token"] = VaultToken
        };
        // both ports are reserved at once: a port released before the process that is to listen on it has started could otherwise be handed out again, and the
        // broker could take the gateway's port, answering its health checks while the gateway fails to start
        var ports = GetFreePorts(2);
        var (gatewayPort, brokerPort) = (ports[0], ports[1]);
        this.GatewayUri = new($"http://127.0.0.1:{gatewayPort}/");
        var gateway = this.Start("gateway", "src/gateway/CloudStreams.Gateway.Api", "CloudStreams.Gateway.Api", gatewayPort, new(environment) { ["CLOUDSTREAMS_GATEWAY_NAME"] = "gateway-e2e" });
        var broker = this.Start("broker", "src/broker/CloudStreams.Broker.Api", "CloudStreams.Broker.Api", brokerPort, new(environment) { ["CLOUDSTREAMS_BROKER_NAME"] = "broker-e2e", ["Logging__LogLevel__CloudStreams"] = "Debug", ["Logging__LogLevel__System.Net.Http"] = "Warning" });
        await Task.WhenAll(gateway.WaitUntilReadyAsync(new($"http://127.0.0.1:{gatewayPort}/healthz")), broker.WaitUntilReadyAsync(new($"http://127.0.0.1:{brokerPort}/healthz")));
        // a process that failed to start, e.g. because its port was taken, may have answered nothing at all: whatever answered its health check was not it
        gateway.EnsureRunning();
        broker.EnsureRunning();
        this.Gateway = new HttpClient() { BaseAddress = this.GatewayUri };
        var services = new ServiceCollection().AddLogging().AddSingleton<IJsonSerializer>(Neuroglia.Serialization.Json.JsonSerializer.Default).BuildServiceProvider();
        this.Api = new CloudStreamsCoreApiClient(services, NullLoggerFactory.Instance, Neuroglia.Serialization.Json.JsonSerializer.Default, new HttpClient() { BaseAddress = this.GatewayUri });
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        foreach (var process in this._processes) process.Dispose();
        this.Gateway?.Dispose();
        await this.Subscriber.DisposeAsync();
        await Task.WhenAll(this._redis.DisposeAsync().AsTask(), this._eventStore.DisposeAsync().AsTask(), this._kafka.DisposeAsync().AsTask(), this._rabbitMq.DisposeAsync().AsTask(), this._nats.DisposeAsync().AsTask(), this._vault.DisposeAsync().AsTask());
    }

    /// <summary>
    /// Waits until the specified partition holds at least the specified amount of cloud events
    /// </summary>
    /// <param name="type">The type of the partition</param>
    /// <param name="id">The id of the partition</param>
    /// <param name="count">The amount of cloud events to wait for</param>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    /// <remarks>
    /// Partitions are filled by an EventStoreDB projection, which lags behind the ingestion of cloud events, the more so as the machine is busy: a test that reads a
    /// partition, or that resolves a position in it, right after publishing must wait for the projection to catch up
    /// </remarks>
    public async Task WaitForPartitionAsync(CloudEventPartitionType type, string id, int count)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            try
            {
                var events = await (await this.Api.CloudEvents.Stream.ReadStreamAsync(new(new PartitionReference(type, id)), null, timeout.Token)).CountAsync(timeout.Token);
                if (events >= count) return;
            }
            catch (Neuroglia.ProblemDetailsException ex) when (ex.Problem.Status == 404) { }
            await Task.Delay(100, timeout.Token);
        }
    }

    /// <summary>
    /// Marks the current end of the logs of the gateway and the broker
    /// </summary>
    /// <returns>A checkpoint to pass to <see cref="GetLogs(IReadOnlyList{int}?)"/>, to get the logs written after it</returns>
    public IReadOnlyList<int> CheckpointLogs() => [.. this._processes.Select(p => p.LogLength)];

    /// <summary>
    /// Gets the logs of the gateway and the broker, to explain a failure
    /// </summary>
    /// <param name="since">The checkpoint after which to get the logs, if any</param>
    /// <returns>The logs of the gateway and the broker</returns>
    public string GetLogs(IReadOnlyList<int>? since = null) => string.Join(Environment.NewLine, this._processes.Select((p, i) => p.GetLogs(since?[i] ?? 0)));

    ApplicationProcess Start(string name, string projectDirectory, string assemblyName, int port, Dictionary<string, string> environment)
    {
        var assembly = Path.Combine(FindRepositoryRoot(), projectDirectory, "bin", GetConfiguration(), "net9.0", assemblyName + ".dll");
        if (!File.Exists(assembly)) throw new FileNotFoundException($"The {name} has not been built: '{assembly}' does not exist", assembly);
        var process = new ApplicationProcess(name, assembly, new(environment) { ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}" });
        this._processes.Add(process);
        return process;
    }

    /// <summary>
    /// Gets the specified amount of distinct free ports, which are all held while they are chosen
    /// </summary>
    static int[] GetFreePorts(int count)
    {
        var listeners = Enumerable.Range(0, count).Select(_ => new TcpListener(IPAddress.Loopback, 0)).ToList();
        try
        {
            foreach (var listener in listeners) listener.Start();
            return [.. listeners.Select(l => ((IPEndPoint)l.LocalEndpoint).Port)];
        }
        finally
        {
            foreach (var listener in listeners) listener.Stop();
        }
    }

    static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CloudStreams.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Failed to find the root of the repository");
    }

    // the tests are built in the same configuration as the applications: bin/{configuration}/net9.0
    static string GetConfiguration() => new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Parent!.Name;

    /// <summary>
    /// Represents a Cloud Streams application running in a child process
    /// </summary>
    sealed class ApplicationProcess
        : IDisposable
    {

        readonly string _name;
        readonly Process _process;
        readonly StringBuilder _logs = new();

        public ApplicationProcess(string name, string assembly, Dictionary<string, string> environment)
        {
            this._name = name;
            var startInfo = new ProcessStartInfo("dotnet", $"\"{assembly}\"")
            {
                WorkingDirectory = Path.GetDirectoryName(assembly)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var (key, value) in environment) startInfo.Environment[key] = value;
            this._process = new Process() { StartInfo = startInfo };
            this._process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (this._logs) this._logs.AppendLine(e.Data); };
            this._process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (this._logs) this._logs.AppendLine(e.Data); };
            this._process.Start();
            this._process.BeginOutputReadLine();
            this._process.BeginErrorReadLine();
        }

        public async Task WaitUntilReadyAsync(Uri healthCheck)
        {
            using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds(2) };
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (DateTime.UtcNow < deadline)
            {
                if (this._process.HasExited) throw new InvalidOperationException($"The {this._name} exited with code {this._process.ExitCode}:{Environment.NewLine}{this.GetLogs()}");
                try
                {
                    if ((await http.GetAsync(healthCheck)).IsSuccessStatusCode) return;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { }
                await Task.Delay(250);
            }
            throw new TimeoutException($"The {this._name} was not ready within 90 seconds:{Environment.NewLine}{this.GetLogs()}");
        }

        /// <summary>
        /// Ensures that the process is still running, which it is not if it failed to start after something else answered its health check
        /// </summary>
        public void EnsureRunning()
        {
            // a host that fails to bind its port stops within moments of starting its hosted services
            Thread.Sleep(500);
            if (this._process.HasExited) throw new InvalidOperationException($"The {this._name} exited with code {this._process.ExitCode} after its health check succeeded, which another process must have answered:{Environment.NewLine}{this.GetLogs()}");
        }

        public int LogLength { get { lock (this._logs) return this._logs.Length; } }

        public string GetLogs(int since = 0)
        {
            lock (this._logs) return $"===== {this._name} ====={Environment.NewLine}{this._logs.ToString(since, this._logs.Length - since)}";
        }

        public void Dispose()
        {
            try
            {
                if (!this._process.HasExited) this._process.Kill(entireProcessTree: true);
                this._process.WaitForExit(10_000);
            }
            catch (InvalidOperationException) { }
            this._process.Dispose();
        }

    }

}

/// <summary>
/// Groups the end-to-end tests, which share a single <see cref="CloudStreamsFixture"/>
/// </summary>
[CollectionDefinition(Name)]
public sealed class CloudStreamsCollection
    : ICollectionFixture<CloudStreamsFixture>
{

    /// <summary>
    /// Gets the name of the collection
    /// </summary>
    public const string Name = "Cloud Streams";

}
