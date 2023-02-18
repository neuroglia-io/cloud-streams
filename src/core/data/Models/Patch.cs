namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Describes a patch
/// </summary>
[DataContract]
public class Patch
{

    /// <summary>
    /// Initializes a new <see cref="Patch"/>
    /// </summary>
    public Patch() { }

    /// <summary>
    /// Initializes a new <see cref="Patch"/>
    /// </summary>
    /// <param name="type">The type of patch to apply</param>
    /// <param name="document">The patch document</param>
    public Patch(string type, object document)
    {
        this.Type = type;
        this.Document = document;
    }

    /// <summary>
    /// Gets/sets the patch's type<para></para>
    /// See <see cref="PatchType"/>s
    /// </summary>
    [Required, JsonRequired, MinLength(1)]
    [DataMember(Order = 1, Name = "type", IsRequired = true), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual string Type { get; set; } = null!;

    /// <summary>
    /// Gets/sets the patch document
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "document", IsRequired = true), JsonPropertyName("document"), YamlMember(Alias = "document")]
    public virtual object Document { get; set; } = null!;

}
