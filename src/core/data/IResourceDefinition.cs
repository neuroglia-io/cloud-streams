using CloudStreams.Core.Data.Models;

namespace CloudStreams.Core;

/// <summary>
/// Defines the fundamentals of a definition of a resource type
/// </summary>
public interface IResourceDefinition
    : IMetadata<ResourceMetadata>, ISpec<ResourceDefinitionSpec>
{



}