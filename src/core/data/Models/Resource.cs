namespace CloudStreams.Data.Models;

/// <summary>
/// Represents the base class of all cloud stream resources
/// </summary>
public abstract class Resource
    : IResource
{

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    /// <param name="type">An object used to describe the <see cref="Resource"/>'s type</param>
    protected Resource(ResourceType type) 
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        this.Type = type;
    }

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    /// <param name="metadata">The object that describes the resource</param>
    protected Resource(ResourceType type, ResourceMetadata metadata)
        : this(type)
    {
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
        this.Metadata = metadata;
    }

    /// <summary>
    /// Gets/sets the object that describes the resource
    /// </summary>
    [DataMember(Order = 1, Name = "metadata"), JsonPropertyName("metadata"), YamlMember(Alias = "metadata")]
    public virtual ResourceMetadata Metadata { get; set; } = null!;

    object IMetadata.Metadata => this.Metadata;

    /// <inheritdoc/>
    [IgnoreDataMember, JsonIgnore, YamlIgnore]
    public virtual ResourceType Type { get; }

}

/// <summary>
/// Represents the base class of all cloud stream resources
/// </summary>
/// <typeparam name="TSpec">The type of the resource's spec</typeparam>
public abstract class Resource<TSpec>
    : Resource, IResource<TSpec>
    where TSpec : class, new()
{

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    /// <param name="type">An object used to describe the <see cref="Resource"/>'s type</param>
    protected Resource(ResourceType type) : base(type) { }

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    /// <param name="type">An object used to describe the resource's type</param>
    /// <param name="metadata">The object that describes the resource</param>
    /// <param name="spec">The resource's spec</param>
    protected Resource(ResourceType type, ResourceMetadata metadata, TSpec spec)
        : base(type, metadata)
    {
        if(spec == null) throw new ArgumentNullException(nameof(spec));
        this.Spec = spec;
    }

    /// <summary>
    /// Gets/sets the object used to define and configure the resource
    /// </summary>
    [DataMember(Order = 1, Name = "spec"), JsonPropertyName("spec"), YamlMember(Alias = "spec")]
    public virtual TSpec Spec { get; set; } = null!;

    object ISpec.Spec => this.Spec;

}


/// <summary>
/// Represents the base class of all cloud stream resources
/// </summary>
/// <typeparam name="TSpec">The type of the resource's spec</typeparam>
public abstract class Resource<TSpec, TStatus>
    : Resource<TSpec>, IResource<TSpec, TStatus>
    where TSpec : class, new()
    where TStatus : class, new()
{

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    /// <param name="type">An object used to describe the <see cref="Resource"/>'s type</param>
    protected Resource(ResourceType type) : base(type) { }

    /// <summary>
    /// Initializes a new <see cref="Resource"/>
    /// </summary>
    /// <param name="type">An object used to describe the resource's type</param>
    /// <param name="metadata">The object that describes the resource</param>
    /// <param name="spec">The resource's spec</param>
    /// <param name="status">An object that describes the resource's status</param>
    protected Resource(ResourceType type, ResourceMetadata metadata, TSpec spec, TStatus? status = null)
        : base(type, metadata, spec)
    {
        this.Status = status;
    }

    /// <summary>
    /// Gets/sets an object that describes the resource's status, if any
    /// </summary>
    [DataMember(Order = 1, Name = "status"), JsonPropertyName("status"), YamlMember(Alias = "status")]
    public virtual TStatus? Status { get; set; }

    object? IStatus.Status => this.Status;

}