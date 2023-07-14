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

using CloudStreams.Core.Data;
using Hylo;
using Hylo.Api.Application;
using Hylo.Api.Application.Commands.Resources.Generic;
using Hylo.Api.Application.Queries.Resources.Generic;
using Hylo.Infrastructure.Services;

namespace CloudStreams.Core.Application;

/// <summary>
/// Defines extensions for <see cref="IServiceCollection"/>s
/// </summary>
public static class IServiceCollectionExtensions
{

    /// <summary>
    /// Registers and configures generic query handlers
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddGenericQueryHandlers(this IServiceCollection services)
    {
        foreach (Type queryableType in typeof(Broker).Assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType && !t.IsInterface && typeof(Hylo.Resource).IsAssignableFrom(t)))
        {
            var serviceLifetime = ServiceLifetime.Scoped;
            Type queryType;
            Type resultType;
            Type handlerServiceType;
            Type handlerImplementationType;

            queryType = typeof(GetResourceQuery<>).MakeGenericType(queryableType);
            resultType = typeof(ApiResponse<>).MakeGenericType(queryableType);
            handlerServiceType = typeof(MediatR.IRequestHandler<,>).MakeGenericType(queryType, resultType);
            handlerImplementationType = typeof(GetResourceQueryHandler<>).MakeGenericType(queryableType);
            services.Add(new ServiceDescriptor(handlerServiceType, handlerImplementationType, serviceLifetime));

            queryType = typeof(GetResourceDefinitionQuery<>).MakeGenericType(queryableType);
            resultType = typeof(ApiResponse<IResourceDefinition>);
            handlerServiceType = typeof(MediatR.IRequestHandler<,>).MakeGenericType(queryType, resultType);
            handlerImplementationType = typeof(GetResourceDefinitionQueryHandler<>).MakeGenericType(queryableType);
            services.Add(new ServiceDescriptor(handlerServiceType, handlerImplementationType, serviceLifetime));

            queryType = typeof(GetResourcesQuery<>).MakeGenericType(queryableType);
            resultType = typeof(ApiResponse<>).MakeGenericType(typeof(IAsyncEnumerable<>).MakeGenericType(queryableType));
            handlerServiceType = typeof(MediatR.IRequestHandler<,>).MakeGenericType(queryType, resultType);
            handlerImplementationType = typeof(GetResourcesQueryHandler<>).MakeGenericType(queryableType);
            services.Add(new ServiceDescriptor(handlerServiceType, handlerImplementationType, serviceLifetime));

            queryType = typeof(ListResourcesQuery<>).MakeGenericType(queryableType);
            resultType = typeof(ApiResponse<>).MakeGenericType(typeof(Hylo.ICollection<>).MakeGenericType(queryableType));
            handlerServiceType = typeof(MediatR.IRequestHandler<,>).MakeGenericType(queryType, resultType);
            handlerImplementationType = typeof(ListResourcesQueryHandler<>).MakeGenericType(queryableType);
            services.Add(new ServiceDescriptor(handlerServiceType, handlerImplementationType, serviceLifetime));

            queryType = typeof(WatchResourcesQuery<>).MakeGenericType(queryableType);
            resultType = typeof(ApiResponse<>).MakeGenericType(typeof(IResourceWatch<>).MakeGenericType(queryableType));
            handlerServiceType = typeof(MediatR.IRequestHandler<,>).MakeGenericType(queryType, resultType);
            handlerImplementationType = typeof(WatchResourcesQueryHandler<>).MakeGenericType(queryableType);
            services.Add(new ServiceDescriptor(handlerServiceType, handlerImplementationType, serviceLifetime));
        }
        return services;
    }

    /// <summary>
    /// Registers and configures generic command handlers
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure</param>
    /// <returns>The configured <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddGenericCommandHandlers(this IServiceCollection services)
    {
        foreach (Type resourceType in typeof(Broker).Assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType && !t.IsInterface && typeof(Hylo.Resource).IsAssignableFrom(t)))
        {
            var serviceLifetime = ServiceLifetime.Scoped;
            Type commandType;
            Type resultType;
            Type handlerServiceType;
            Type handlerImplementationType;

            commandType = typeof(CreateResourceCommand<>).MakeGenericType(resourceType);
            resultType = typeof(ApiResponse<>).MakeGenericType(resourceType);
            handlerServiceType = typeof(MediatR.IRequestHandler<,>).MakeGenericType(commandType, resultType);
            handlerImplementationType = typeof(CreateResourceCommandHandler<>).MakeGenericType(resourceType);
            services.Add(new ServiceDescriptor(handlerServiceType, handlerImplementationType, serviceLifetime));

            commandType = typeof(ReplaceResourceCommand<>).MakeGenericType(resourceType);
            resultType = typeof(ApiResponse<>).MakeGenericType(resourceType);
            handlerServiceType = typeof(MediatR.IRequestHandler<,>).MakeGenericType(commandType, resultType);
            handlerImplementationType = typeof(ReplaceResourceCommandHandler<>).MakeGenericType(resourceType);
            services.Add(new ServiceDescriptor(handlerServiceType, handlerImplementationType, serviceLifetime));

            commandType = typeof(PatchResourceCommand<>).MakeGenericType(resourceType);
            resultType = typeof(ApiResponse<>).MakeGenericType(resourceType);
            handlerServiceType = typeof(MediatR.IRequestHandler<,>).MakeGenericType(commandType, resultType);
            handlerImplementationType = typeof(PatchResourceCommandHandler<>).MakeGenericType(resourceType);
            services.Add(new ServiceDescriptor(handlerServiceType, handlerImplementationType, serviceLifetime));

            commandType = typeof(DeleteResourceCommand<>).MakeGenericType(resourceType);
            resultType = typeof(ApiResponse<>).MakeGenericType(resourceType);
            handlerServiceType = typeof(MediatR.IRequestHandler<,>).MakeGenericType(commandType, resultType);
            handlerImplementationType = typeof(DeleteResourceCommandHandler<>).MakeGenericType(resourceType);
            services.Add(new ServiceDescriptor(handlerServiceType, handlerImplementationType, serviceLifetime));

        }
        return services;
    }

}
