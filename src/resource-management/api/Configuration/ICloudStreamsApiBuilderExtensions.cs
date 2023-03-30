// Copyright © 2023-Present The Cloud Streams Authors
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
using CloudStreams.Core.Data.Models;
using CloudStreams.Core.Infrastructure.Configuration;
using CloudStreams.ResourceManagement.Api.Controllers;
using CloudStreams.ResourceManagement.Api.Services;
using CloudStreams.ResourceManagement.Application.Commands.Generic;
using CloudStreams.ResourceManagement.Application.Queries.Generic;
using MediatR;

namespace CloudStreams.ResourceManagement.Api.Configuration;

/// <summary>
/// Defines extensions for <see cref="ICloudStreamsApplicationBuilder"/>s
/// </summary>
public static class ICloudStreamsApiBuilderExtensions
{

    /// <summary>
    /// Configures CloudStreams to use the Resource Management API
    /// </summary>
    /// <param name="builder">The <see cref="ICloudStreamsApplicationBuilder"/> to configure</param>
    /// <returns>The configured <see cref="ICloudStreamsApplicationBuilder"/></returns>
    public static ICloudStreamsApplicationBuilder UseResourceManagementApi(this ICloudStreamsApplicationBuilder builder)
    {
        builder.RegisterApplicationPart<GatewaysController>();
        builder.RegisterMediationAssembly<CreateResourceCommand<Gateway>>();
        builder.Services.AddSingleton<ResourceWatchEventHubController>();
        builder.Services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<ResourceWatchEventHubController>());
        foreach (var resource in TypeCacheUtil.FindFilteredTypes("cs:resources", t => t.IsClass && !t.IsAbstract && !t.IsInterface && typeof(IResource).IsAssignableFrom(t)))
        {
            var queryType = typeof(CreateResourceCommand<>).MakeGenericType(resource);
            var resultType = typeof(Response<>).MakeGenericType(resource);
            var serviceType = typeof(IRequestHandler<,>).MakeGenericType(queryType, resultType);
            var implementationType = typeof(CreateResourceCommandHandler<>).MakeGenericType(resource);
            builder.Services.AddTransient(serviceType, implementationType);

            queryType = typeof(GetResourceDefinitionQuery<>).MakeGenericType(resource);
            resultType = typeof(Response<IResourceDefinition>);
            serviceType = typeof(IRequestHandler<,>).MakeGenericType(queryType, resultType);
            implementationType = typeof(GetResourceDefinitionQueryHandler<>).MakeGenericType(resource);
            builder.Services.AddTransient(serviceType, implementationType);

            queryType = typeof(GetResourceQuery<>).MakeGenericType(resource);
            resultType = typeof(Response<>).MakeGenericType(resource);
            serviceType = typeof(IRequestHandler<,>).MakeGenericType(queryType, resultType);
            implementationType = typeof(GetResourceQueryHandler<>).MakeGenericType(resource);
            builder.Services.AddTransient(serviceType, implementationType);

            queryType = typeof(ListResourceQuery<>).MakeGenericType(resource);
            resultType = typeof(Response<>).MakeGenericType(typeof(IAsyncEnumerable<>).MakeGenericType(resource));
            serviceType = typeof(IRequestHandler<,>).MakeGenericType(queryType, resultType);
            implementationType = typeof(ListResourceQueryHandler<>).MakeGenericType(resource);
            builder.Services.AddTransient(serviceType, implementationType);

            queryType = typeof(PatchResourceCommand<>).MakeGenericType(resource);
            resultType = typeof(Response<>).MakeGenericType(resource);
            serviceType = typeof(IRequestHandler<,>).MakeGenericType(queryType, resultType);
            implementationType = typeof(PatchResourceCommandHandler<>).MakeGenericType(resource);
            builder.Services.AddTransient(serviceType, implementationType);

            queryType = typeof(PatchResourceStatusCommand<>).MakeGenericType(resource);
            resultType = typeof(Response<>).MakeGenericType(resource);
            serviceType = typeof(IRequestHandler<,>).MakeGenericType(queryType, resultType);
            implementationType = typeof(PatchResourceStatusCommandHandler<>).MakeGenericType(resource);
            builder.Services.AddTransient(serviceType, implementationType);

            queryType = typeof(PutResourceCommand<>).MakeGenericType(resource);
            resultType = typeof(Response<>).MakeGenericType(resource);
            serviceType = typeof(IRequestHandler<,>).MakeGenericType(queryType, resultType);
            implementationType = typeof(PutResourceCommandHandler<>).MakeGenericType(resource);
            builder.Services.AddTransient(serviceType, implementationType);

            queryType = typeof(DeleteResourceCommand<>).MakeGenericType(resource);
            resultType = typeof(Response);
            serviceType = typeof(IRequestHandler<,>).MakeGenericType(queryType, resultType);
            implementationType = typeof(DeleteResourceCommandHandler<>).MakeGenericType(resource);
            builder.Services.AddTransient(serviceType, implementationType);
        }
        return builder;
    }

}
