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

using CloudStreams.Core.Resources;
using CloudStreams.Core.Transports.Authentication;
using CloudStreams.Core.Transports.Secrets;
using CloudStreams.Core.Transports.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace CloudStreams.Core.Transports;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds the transports, other than HTTP, that cloud events are delivered over and consumed from (Kafka, NATS, AMQP and other Cloud Streams gateways), the client that
    /// delivers cloud events to endpoints and sinks, and the services that authenticate to them, with secrets read from the configured providers (see #218)
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="configuration">The current <see cref="IConfiguration"/>, which holds the <see cref="SecretOptions"/></param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddCloudStreamsTransports(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        // registering the transports twice, e.g. by the gateway and by the core API it hosts, would register every sink twice
        if (services.Any(s => s.ServiceType == typeof(KafkaTransport))) return services;
        services.Configure<SecretOptions>(configuration.GetSection(SecretOptions.ConfigurationSection));
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(OAuth2TokenClient.HttpClientName);
        services.AddHttpClient(VaultSecretProvider.HttpClientName);
        services.AddHttpClient(KubernetesSecretProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(provider =>
        {
            var directory = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SecretOptions>>().Value.Kubernetes.ServiceAccountDirectory;
            return new HttpClientHandler() { ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) => ValidateKubernetesCertificate(directory, certificate, chain, errors) };
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISecretProvider, EnvironmentSecretProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISecretProvider, FileSecretProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISecretProvider, KubernetesSecretProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISecretProvider, VaultSecretProvider>());
        services.TryAddSingleton<ISecretStore, SecretStore>();
        services.TryAddSingleton<IOAuth2TokenClient, OAuth2TokenClient>();
        services.TryAddSingleton<ICredentialResolver, CredentialResolver>();
        services.TryAddSingleton<IEndpointResolver, EndpointResolver>();
        services.AddHttpClient<CloudEventDeliveryClient>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddHttpClient(CloudStreamsBridge.HttpClientName);
        services.TryAddSingleton<KafkaTransport>();
        services.TryAddSingleton<NatsTransport>();
        services.TryAddSingleton<AmqpTransport>();
        services.TryAddSingleton<CloudStreamsBridge>();
        services.AddSingleton<ICloudEventSink>(provider => provider.GetRequiredService<KafkaTransport>());
        services.AddSingleton<ICloudEventSink>(provider => provider.GetRequiredService<NatsTransport>());
        services.AddSingleton<ICloudEventSink>(provider => provider.GetRequiredService<AmqpTransport>());
        services.AddSingleton<ICloudEventSink>(provider => provider.GetRequiredService<CloudStreamsBridge>());
        services.AddSingleton<ICloudEventFeedConsumer>(provider => provider.GetRequiredService<KafkaTransport>());
        services.AddSingleton<ICloudEventFeedConsumer>(provider => provider.GetRequiredService<NatsTransport>());
        services.AddSingleton<ICloudEventFeedConsumer>(provider => provider.GetRequiredService<AmqpTransport>());
        return services;
    }

    /// <summary>
    /// Validates the certificate of the Kubernetes API against the cluster's certificate authority, which is mounted with the pod's service account
    /// </summary>
    static bool ValidateKubernetesCertificate(string serviceAccountDirectory, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None) return true;
        var authorityPath = Path.Combine(serviceAccountDirectory, "ca.crt");
        if (certificate == null || chain == null || !File.Exists(authorityPath)) return false;
        using var authority = X509CertificateLoader.LoadCertificateFromFile(authorityPath);
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(authority);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(certificate) && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None;
    }

}
