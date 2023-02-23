using YamlDotNet.Serialization;

namespace CloudStreams.Core;

/// <summary>
/// Describes the type of a resource
/// </summary>
[DataContract]
public class ResourceType
    : ValueObject<ResourceType>
{

    /// <summary>
    /// Initializes a new <see cref="ResourceType"/>
    /// </summary>
    public ResourceType() { }

    /// <summary>
    /// Initializes a new <see cref="ResourceType"/>
    /// </summary>
    /// <param name="group">The API group the resource type belongs to</param>
    /// <param name="version">The resource type's version</param>
    /// <param name="plural">The resource type's plural name</param>
    /// <param name="kind">The resource type's kind</param>
    public ResourceType(string group, string version, string plural, string kind)
    {
        if (string.IsNullOrWhiteSpace(group)) throw new ArgumentNullException(nameof(group));
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentNullException(nameof(version));
        if (string.IsNullOrWhiteSpace(plural)) throw new ArgumentNullException(nameof(plural));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentNullException(nameof(kind));
        this.Group = group;
        this.Version = version;
        this.Plural = plural;
        this.Kind = kind;
    }

    /// <summary>
    /// Gets/sets the API group the resource type belongs to
    /// </summary>
    [DataMember(Order = 1, Name = "group"), JsonPropertyName("group"), YamlMember(Alias = "group")]
    public virtual string Group { get; set; } = null!;

    /// <summary>
    /// Gets/sets resource type's version
    /// </summary>
    [DataMember(Order = 2, Name = "version"), JsonPropertyName("version"), YamlMember(Alias = "version")]
    public virtual string Version { get; set; } = null!;

    /// <summary>
    /// Gets/sets the resource type's plural name
    /// </summary>
    [DataMember(Order = 3, Name = "plural"), JsonPropertyName("plural"), YamlMember(Alias = "plural")]
    public virtual string Plural { get; set; } = null!;

    /// <summary>
    /// Gets/sets the resource kind
    /// </summary>
    [DataMember(Order = 4, Name = "kind"), JsonPropertyName("kind"), YamlMember(Alias = "kind")]
    public virtual string Kind { get; set; } = null!;

    /// <inheritdoc/>
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return this.Group;
        yield return this.Version;
        yield return this.Plural;
        yield return this.Kind;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{this.Plural}.{this.Group}";

}

/// <summary>
/// Represents the base class for all <see cref="ValueObject{T}"/>s
/// </summary>
/// <typeparam name="T">The type of the <see cref="ValueObject{T}"/></typeparam>
public abstract class ValueObject<T>
    : IEquatable<ValueObject<T>>
    where T : ValueObject<T>
{

    /// <inheritdoc/>
    public bool Equals(ValueObject<T>? other)
    {
        if (ReferenceEquals(this, null) ^ ReferenceEquals(other, null)) return false;
        return ReferenceEquals(this, other) || this.Equals(other);
    }

    /// <inheritdoc/>
    public override bool Equals(object? other)
    {
        if (other is ValueObject<T> valueObject) return this.Equals(valueObject);
        else return false;
    }

    /// <summary>
    /// Gets an <see cref="IEnumerable{T}"/> containing the components use to generate the <see cref="ValueObject{T}"/>'s hashcode
    /// </summary>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing the components use to generate the <see cref="ValueObject{T}"/>'s hashcode</returns>
    protected abstract IEnumerable<object> GetEqualityComponents();

    /// <inheritdoc/>
    public override int GetHashCode() => GetEqualityComponents().Select(x => x != null ? x.GetHashCode() : 0).Aggregate((x, y) => x ^ y);

    /// <summary>
    /// Determines whether the two specified <see cref="ValueObject{T}"/>s are equal
    /// </summary>
    /// <param name="type1">The first <see cref="ValueObject{T}"/></param>
    /// <param name="type2">The second <see cref="ValueObject{T}"/></param>
    /// <returns>A boolean indicating whether or not the two specified <see cref="ValueObject{T}"/>s are equal</returns>
    public static bool operator ==(ValueObject<T>? type1, ValueObject<T>? type2) => type1?.Equals(type2) == true;

    /// <summary>
    /// Determines whether the two specified <see cref="ValueObject{T}"/>s are not equal
    /// </summary>
    /// <param name="type1">The first <see cref="ValueObject{T}"/></param>
    /// <param name="type2">The second <see cref="ValueObject{T}"/></param>
    /// <returns>A boolean indicating whether or not the two specified <see cref="ValueObject{T}"/>s are not equal</returns>
    public static bool operator !=(ValueObject<T>? type1, ValueObject<T>? type2) => type1?.Equals(type2) == false;

}