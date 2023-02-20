namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents a label-based resource selector
/// </summary>
[DataContract]
public class ResourceLabelSelector
{

    /// <summary>
    /// Initializes a new <see cref="ResourceLabelSelector"/>
    /// </summary>
    public ResourceLabelSelector() { }

    /// <summary>
    /// Initializes a new <see cref="ResourceLabelSelector"/>
    /// </summary>
    /// <param name="key">The key of the label to select resources by</param>
    /// <param name="operator">The selection operator</param>
    /// <param name="values">The expected values, if any</param>
    public ResourceLabelSelector(string key, ResourceLabelSelectionOperator @operator, params string[] values)
    {
        this.Key = key;
        this.Operator = @operator;
        if (values == null) return;
        if (values.Length == 1) this.Value = values[0];
        else this.Values = values.ToList();
    }

    /// <summary>
    /// Gets/sets the key of the label to select resources by
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "key", IsRequired = true), JsonPropertyName("key"), YamlMember(Alias = "key")]
    public string Key { get; set; } = null!;

    /// <summary>
    /// Gets/sets the selection operator
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "operator", IsRequired = true), JsonPropertyName("operator"), YamlMember(Alias = "operator")]
    public virtual ResourceLabelSelectionOperator Operator { get; set; }

    /// <summary>
    /// Gets/sets the expected value, if any
    /// </summary>
    [DataMember(Order = 3, Name = "value", IsRequired = true), JsonPropertyName("value"), YamlMember(Alias = "value")]
    public virtual string? Value { get; set; }

    /// <summary>
    /// Gets/sets a list containing expected values, if any
    /// </summary>
    [DataMember(Order = 4, Name = "values", IsRequired = true), JsonPropertyName("values"), YamlMember(Alias = "values")]
    public virtual List<string>? Values { get; set; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Operator switch
        {
            ResourceLabelSelectionOperator.Contains => string.IsNullOrWhiteSpace(this.Value) && this.Values?.Any() == false ? this.Key : $"{this.Key} in ({this.Values!.Join(',')})",
            ResourceLabelSelectionOperator.NotContains => string.IsNullOrWhiteSpace(this.Value) && this.Values?.Any() == false ? $"!{this.Key}" : $"{this.Key} notin ({this.Values!.Join(',')})",
            ResourceLabelSelectionOperator.Equals => $"{this.Key}={this.Value}",
            ResourceLabelSelectionOperator.NotEquals => $"{this.Key}!={this.Value}",
            _ => throw new NotSupportedException($"The specified {nameof(ResourceLabelSelectionOperator)} '{this.Operator}' is not supported"),
        };
    }

    /// <summary>
    /// Parses the specified input
    /// </summary>
    /// <param name="input">The input to parse</param>
    /// <returns>A new <see cref="ResourceLabelSelector"/></returns>
    public static ResourceLabelSelector Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new ArgumentNullException(nameof(input));
        if (input.StartsWith('!')) return new(input[1..], ResourceLabelSelectionOperator.NotContains);
        string key;
        var components = input.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        ResourceLabelSelectionOperator selectionOperator;
        if (components.Length == 2)
        {
            key = components[0];
            selectionOperator = ResourceLabelSelectionOperator.Equals;
            if (key.EndsWith('!'))
            {
                key = key[..^1];
                selectionOperator = ResourceLabelSelectionOperator.NotEquals;
            }
            return new(key, selectionOperator, components[1]);
        }
        components = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (components.Length == 1) return new(input, ResourceLabelSelectionOperator.Contains);
        selectionOperator = components[1] switch
        {
            "in" =>  ResourceLabelSelectionOperator.Contains,
            "notin" => ResourceLabelSelectionOperator.NotContains,
            _ => throw new NotSupportedException($"The specified selection operator '{components[2]}' is not supported")
        };
        key = components[0];
        var operatorIndex = input.IndexOf(components[1], key.Length + 1) + components[1].Length + 1;
        components = input[operatorIndex..].Trim().TrimStart('(').TrimEnd(')').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new ResourceLabelSelector(key, selectionOperator, components);
    }

    /// <summary>
    /// Implicitly converts the specified input into a new <see cref="ResourceLabelSelector"/>
    /// </summary>
    /// <param name="input">The input to convert</param>
    public static implicit operator ResourceLabelSelector?(string? input) => string.IsNullOrWhiteSpace(input) ? null : Parse(input);

}
