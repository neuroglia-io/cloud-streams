namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure a cloud event context attribute filter.
/// </summary>
[DataContract]
public record CloudEventAttributeFilter
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventAttributeFilter"/>.
    /// </summary>
    public CloudEventAttributeFilter() { }

    /// <summary>
    /// Initializes a new <see cref="CloudEventAttributeFilter"/>.
    /// </summary>
    /// <param name="name">The name of the cloud event context attribute to filter.</param>
    /// <param name="value">The value of the cloud event context attribute to filter. Not setting any value configures the filter to only check if cloud events defined the attribute, no matter its value.</param>
    public CloudEventAttributeFilter(string name, string? value = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        this.Name = name;
        this.Value = value;
    }

    /// <summary>
    /// Gets/sets the name of the cloud event context attribute to filter.
    /// </summary>
    [Required, MinLength(1)]
    [DataMember(Order = 1, Name = "name", IsRequired = true), JsonPropertyName("name"), YamlMember(Alias = "name")]
    public virtual string Name { get; set; } = null!;

    /// <summary>
    /// Gets/sets the value of the cloud event context attribute to filter. Not setting any value configures the filter to only check if cloud events defined the attribute, no matter its value.
    /// </summary>
    [DataMember(Order = 2, Name = "value", IsRequired = true), JsonPropertyName("value"), YamlMember(Alias = "value")]
    public virtual string? Value { get; set; }

    /// <inheritdoc/>
    public override string ToString() => string.IsNullOrWhiteSpace(this.Value) ? this.Name : $"{this.Name}={this.Value}";

}