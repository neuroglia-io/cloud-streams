using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace CloudStreams.Core.Serialization.Yaml;

/// <summary>
/// Represents the <see cref="INodeDeserializer"/> used to deserialize <see cref="Enum"/> using the <see cref="EnumHelper.Parse{TEnum}(string)"/> method
/// </summary>
public class StringEnumDeserializer
    : INodeDeserializer
{

    /// <summary>
    /// Initializes a new <see cref="StringEnumDeserializer"/>
    /// </summary>
    /// <param name="inner">The inner <see cref="INodeDeserializer"/></param>
    public StringEnumDeserializer(INodeDeserializer inner)
    {
        this.Inner = inner;
    }

    /// <summary>
    /// Gets the inner <see cref="INodeDeserializer"/>
    /// </summary>
    protected INodeDeserializer Inner { get; }

    /// <inheritdoc/>
    public virtual bool Deserialize(IParser reader, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer, out object? value)
    {
        if (!typeof(Enum).IsAssignableFrom(expectedType)) return this.Inner.Deserialize(reader, expectedType, nestedObjectDeserializer, out value);
        if (!this.Inner.Deserialize(reader, typeof(string), nestedObjectDeserializer, out value)) return false;
        var valueStr = (string?)value;
        if (string.IsNullOrWhiteSpace(valueStr)) value = expectedType.GetDefaultValue();
        else value = EnumHelper.Parse(valueStr, expectedType);
        return true;
    }

}
