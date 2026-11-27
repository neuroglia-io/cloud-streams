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

using CloudStreams.Core.Api.Configuration;
using CloudStreams.Core.Api.Services;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;
using Neuroglia.Data.PatchModel.Services;
using StackExchange.Redis;

namespace CloudStreams.Operator.Services;

/// <summary>
/// Runs the one-off upgrade from the Redis mode to the Kubernetes mode, which copies the resources of Redis into Kubernetes (see #283), e.g.
/// <c>dotnet CloudStreams.Operator.dll migrate --redis redis:6379 --context my-cluster --default-namespace cloud-streams</c>
/// </summary>
public static class MigrationCommand
{

    /// <summary>
    /// Gets the name of the command, which the operator's first argument selects
    /// </summary>
    public const string Name = "migrate";

    /// <summary>
    /// Gets the usage of the command
    /// </summary>
    public const string Usage = """
        Copies the namespaces and the resources of the Redis store into the Kubernetes store, with their status, so that subscriptions resume from their offsets.
        Resources that already exist in Kubernetes are left as they are. The CRDs must be installed first.

        Usage: migrate --redis <connection string> [--context <kubeconfig context>] [--kubeconfig <path>] [--default-namespace <namespace>] [--dry-run]
          --redis              the connection string of the Redis that stores the resources
          --context            the context of the kubeconfig to use. Defaults to the current one, or to the pod's service account in a cluster
          --kubeconfig         the path of the kubeconfig to use. Defaults to ~/.kube/config
          --default-namespace  the Kubernetes namespace of the 'default' namespace of Cloud Streams, as the chart's 'resources.kubernetes.defaultNamespace'. Defaults to 'default'
          --dry-run            only checks what would be copied
        """;

    /// <summary>
    /// Runs the command
    /// </summary>
    /// <param name="args">The arguments of the command, without its name</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>The exit code of the command</returns>
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        string? redisConnection = null, context = null, kubeconfig = null, defaultNamespace = null;
        var dryRun = false;
        for (var index = 0; index < args.Length; index++)
        {
            string Value() => index + 1 < args.Length ? args[++index] : throw new ArgumentException($"'{args[index]}' requires a value");
            try
            {
                switch (args[index])
                {
                    case "--redis": redisConnection = Value(); break;
                    case "--context": context = Value(); break;
                    case "--kubeconfig": kubeconfig = Value(); break;
                    case "--default-namespace": defaultNamespace = Value(); break;
                    case "--dry-run": dryRun = true; break;
                    case "--help" or "-h": Console.WriteLine(Usage); return 0;
                    default: throw new ArgumentException($"Unknown argument '{args[index]}'");
                }
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(Usage);
                return 2;
            }
        }
        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            Console.Error.WriteLine("--redis is required");
            Console.Error.WriteLine(Usage);
            return 2;
        }
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddSimpleConsole(console => console.SingleLine = true));
        var logger = loggerFactory.CreateLogger(typeof(MigrationCommand));
        var options = new KubernetesResourceStoreOptions() { DefaultNamespace = defaultNamespace ?? "default", Context = context, KubeConfigPath = kubeconfig, CreateNamespaces = true };
        var store = new ResourceStoreOptions() { Store = ResourceStoreType.Kubernetes, Kubernetes = options };
        try { store.Validate(); }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        var serializer = Neuroglia.Serialization.Json.JsonSerializer.Default;
        IPatchHandler[] patchHandlers = [new JsonMergePatchHandler(), new JsonPatchHandler(), new JsonStrategicMergePatchHandler()];
        var redisOptions = ConfigurationOptions.Parse(redisConnection);
        redisOptions.AbortOnConnectFail = true;
        using var redis = await ConnectionMultiplexer.ConnectAsync(redisOptions).ConfigureAwait(false);
        using var kubernetes = new Kubernetes(KubernetesClientConfigurationFactory.Create(options));
        var source = new AtomicRedisDatabase(loggerFactory, redis, serializer, patchHandlers);
        var target = new KubernetesResourceDatabase(loggerFactory.CreateLogger<KubernetesDatabase>(), kubernetes, serializer, patchHandlers, options);
        await target.InitializeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (copied, existing) = await new RedisMigration(loggerFactory.CreateLogger<RedisMigration>(), source, target, serializer).RunAsync(dryRun, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(dryRun ? "{copied} resource(s) would be copied, {existing} already exist(s)" : "{copied} resource(s) copied, {existing} already existed", copied, existing);
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The migration failed. It may be run again: the resources already copied are skipped");
            return 1;
        }
    }

}
