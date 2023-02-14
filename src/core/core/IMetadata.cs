namespace CloudStreams;

/// <summary>
/// Defines the fundamentals of an object described by metadata
/// </summary>
public interface IMetadata
{

    /// <summary>
    /// Gets the metadata that describes the object
    /// </summary>
    object Metadata { get; }

}

/// <summary>
/// Defines the fundamentals of an object described by metadata
/// </summary>
/// <typeparam name="TMetadata">The type of the metadata</typeparam>
public interface IMetadata<TMetadata>
    : IMetadata
    where TMetadata : class, new()
{

    /// <summary>
    /// Gets the metadata that describes the object
    /// </summary>
    new TMetadata Metadata { get; }

}
