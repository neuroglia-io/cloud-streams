namespace CloudStreams.Core.Data.Attributes;

/// <summary>
/// Represents an <see cref="Attribute"/> used to validate that the value of the marked property falls within range
/// </summary>
/// <typeparam name="T">The type of value to support</typeparam>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class OneOfAttribute<T>
    : ValidationAttribute
{

    /// <summary>
    /// Initializes a new <see cref="OneOfAttribute{T}"/>
    /// </summary>
    /// <param name="supportedValues">A <see cref="List{T}"/> containing all supported values</param>
    public OneOfAttribute(params T[] supportedValues) 
    {
        this.SupportedValues = supportedValues.ToList();
    }

    /// <summary>
    /// Gets a <see cref="List{T}"/> containing all supported values
    /// </summary>
    protected List<T> SupportedValues { get; }

    /// <inheritdoc/>
    public override bool IsValid(object? value)
    {
        if (value == null) return true;
        var result =  value is T t && this.SupportedValues.Contains(t);
        return result;
    }

}
