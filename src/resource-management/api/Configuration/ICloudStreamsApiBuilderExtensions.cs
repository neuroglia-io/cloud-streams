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
