namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents the definition of a <see cref="Subscription"/> <see cref="Resource"/>
/// </summary>
[DataContract]
public record SubscriptionResourceDefinition
    : ResourceDefinition
{

    /// <summary>
    /// Gets the definition of <see cref="ResourceDefinition"/>s
    /// </summary>
    public static new ResourceDefinition Instance { get; set; }
    /// <summary>
    /// Gets/sets the group resource definitions belong to
    /// </summary>
    public static new string ResourceGroup { get; set; } = DefaultResourceGroup;
    /// <summary>
    /// Gets/sets the resource version of resource definitions
    /// </summary>
    public static new string ResourceVersion { get; set; }
    /// <summary>
    /// Gets/sets the plural name of resource definitions
    /// </summary>
    public static new string ResourcePlural { get; set; }
    /// <summary>
    /// Gets/sets the kind of resource definitions
    /// </summary>
    public static new string ResourceKind { get; set; }

    static SubscriptionResourceDefinition()
    {
        using var stream = typeof(Subscription).Assembly.GetManifestResourceStream($"{typeof(Subscription).Namespace}.{nameof(Subscription)}.yaml")!;
        using var streamReader = new StreamReader(stream);
        Instance = YamlSerializer.Default.Deserialize<ResourceDefinition>(streamReader.ReadToEnd())!;
        ResourceGroup = Instance.Spec.Group;
        ResourceVersion = Instance.Spec.Versions.Last().Name;
        ResourcePlural = Instance.Spec.Names.Plural;
        ResourceKind = Instance.Spec.Names.Kind;
    }

    /// <summary>
    /// Initializes a new <see cref="SubscriptionResourceDefinition"/>
    /// </summary>
    public SubscriptionResourceDefinition() : base(Instance) { }

}