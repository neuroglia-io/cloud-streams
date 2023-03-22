using FluentValidation;
using System.Text.RegularExpressions;

namespace CloudStreams.Gateway.Application.Services;

/// <summary>
/// Represents the service used to validate <see cref="CloudEvent"/>s
/// </summary>
public class CloudEventValidator
    : AbstractValidator<CloudEvent>
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventValidator"/>
    /// </summary>
    public CloudEventValidator()
    {
        this.RuleFor(e => e.Id).NotEmpty();
        this.RuleFor(e => e.SpecVersion).NotEmpty();
        this.RuleFor(e => e.Source).NotNull();
        this.RuleFor(e => e.Type).NotEmpty();
        this.RuleForEach(e => e.ToDictionary())
            .Must(BeLowerCaseAlphanumeric)
            .WithName("attributes")
            .WithMessage(Core.Data.Properties.ProblemDetails.CloudEventAttributeNameMustBeAlphanumeric);
    }

    /// <summary>
    /// Determines whether or not the specified attribute's name is an alphanumeric, lowercased value
    /// </summary>
    /// <param name="attribute">The attribute to check</param>
    /// <returns>A boolean indicating whether or not the specified attribute's name is an alphanumeric, lowercased value</returns>
    public virtual bool BeLowerCaseAlphanumeric(KeyValuePair<string, object> attribute) => Regex.IsMatch(attribute.Key, "^[a-z0-9]+$", RegexOptions.Compiled);

}