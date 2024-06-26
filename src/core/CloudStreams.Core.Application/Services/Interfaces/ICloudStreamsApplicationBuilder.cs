﻿// Copyright © 2024-Present The Cloud Streams Authors
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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Defines fundamentals of a service used to build and configure a Cloud Streams API
/// </summary>
public interface ICloudStreamsApplicationBuilder
{

    /// <summary>
    /// Gets the current <see cref="ConfigurationManager"/>
    /// </summary>
    ConfigurationManager Configuration { get; }

    /// <summary>
    /// Gets the current <see cref="IHostEnvironment"/>
    /// </summary>
    IHostEnvironment Environment { get; }

    /// <summary>
    /// Gets the current <see cref="IServiceCollection"/>
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Gets the service used to configure and build logging
    /// </summary>
    ILoggingBuilder Logging { get; }

    /// <summary>
    /// Configures the Cloud Streams application to use the specified service name and version
    /// </summary>
    /// <param name="name">The name of the Cloud Streams application service</param>
    /// <param name="version">The version of the Cloud Streams application service</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    ICloudStreamsApplicationBuilder WithServiceName(string name, string? version = null);

    /// <summary>
    /// Registers an <see cref="Assembly"/> application part
    /// </summary>
    /// <typeparam name="TMarkup">A markup type containing by the <see cref="Assembly"/> application part to add</typeparam>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    ICloudStreamsApplicationBuilder RegisterApplicationPart<TMarkup>();

    /// <summary>
    /// Registers an <see cref="Assembly"/> to scan for mediation components such as queries, commands, etc.
    /// </summary>
    /// <typeparam name="TMarkup">A markup type containing by the <see cref="Assembly"/> to scan</typeparam>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    ICloudStreamsApplicationBuilder RegisterMediationAssembly<TMarkup>();

    /// <summary>
    /// Registers an <see cref="Assembly"/> to scan for fluent validators
    /// </summary>
    /// <typeparam name="TMarkup">A markup type containing by the <see cref="Assembly"/> to scan</typeparam>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    ICloudStreamsApplicationBuilder RegisterValidationAssembly<TMarkup>();

    /// <summary>
    /// Registers a new health check
    /// </summary>
    /// <param name="setup">An <see cref="Action{T}"/> used to setup the health check to register</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    ICloudStreamsApplicationBuilder RegisterHealthCheck(Action<IHealthChecksBuilder> setup);

    /// <summary>
    /// Builds the Cloud Streams API
    /// </summary>
    void Build();

}
