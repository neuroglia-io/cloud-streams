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

using CloudStreams.Core.Transports.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudStreams.Core.Transports;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Adds the transports, other than HTTP, that cloud events are delivered over and consumed from: Kafka, NATS, AMQP and other Cloud Streams gateways (see #218)
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <param name="configuration">The current <see cref="IConfiguration"/>, which holds the <see cref="TransportOptions"/></param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddCloudStreamsTransports(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<TransportOptions>(configuration.GetSection(TransportOptions.ConfigurationSection));
        services.AddHttpClient(CloudStreamsBridge.HttpClientName);
        services.TryAddSingleton<KafkaTransport>();
        services.TryAddSingleton<NatsTransport>();
        services.TryAddSingleton<AmqpTransport>();
        services.TryAddSingleton<CloudStreamsBridge>();
        services.AddSingleton<ICloudEventSink>(provider => provider.GetRequiredService<KafkaTransport>());
        services.AddSingleton<ICloudEventSink>(provider => provider.GetRequiredService<NatsTransport>());
        services.AddSingleton<ICloudEventSink>(provider => provider.GetRequiredService<AmqpTransport>());
        services.AddSingleton<ICloudEventSink>(provider => provider.GetRequiredService<CloudStreamsBridge>());
        services.AddSingleton<ICloudEventChannelConsumer>(provider => provider.GetRequiredService<KafkaTransport>());
        services.AddSingleton<ICloudEventChannelConsumer>(provider => provider.GetRequiredService<NatsTransport>());
        services.AddSingleton<ICloudEventChannelConsumer>(provider => provider.GetRequiredService<AmqpTransport>());
        return services;
    }

}
