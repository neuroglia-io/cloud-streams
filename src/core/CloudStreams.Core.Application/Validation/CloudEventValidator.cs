// Copyright © 2024-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using FluentValidation;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace CloudStreams.Core.Application.Validation;

/// <summary>
/// Represents the service used to validate <see cref="CloudEvent"/>s against the <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md">CloudEvents specification</see>
/// </summary>
public partial class CloudEventValidator
    : AbstractValidator<CloudEvent>
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventValidator"/>
    /// </summary>
    public CloudEventValidator()
    {
        this.RuleFor(e => e.SpecVersion)
            .NotEmpty()
            .Equal(CloudEventSpecVersion.V1.Version)
            .WithMessage($"The 'specversion' attribute must be '{CloudEventSpecVersion.V1.Version}'");
        this.RuleFor(e => e.Id)
            .NotEmpty()
            .WithMessage("The 'id' attribute is required, and must be a non-empty string");
        this.RuleFor(e => e.Source)
            .NotNull()
            .Must(source => !string.IsNullOrWhiteSpace(source?.OriginalString))
            .WithMessage("The 'source' attribute is required, and must be a non-empty URI-reference");
        this.RuleFor(e => e.Type)
            .NotEmpty()
            .WithMessage("The 'type' attribute is required, and must be a non-empty string");
        this.RuleFor(e => e.Subject)
            .NotEmpty()
            .When(e => e.Subject != null)
            .WithMessage("The 'subject' attribute, if present, must be a non-empty string");
        this.RuleFor(e => e.DataContentType)
            .Must(contentType => MediaTypeHeaderValue.TryParse(contentType, out _))
            .When(e => e.DataContentType != null)
            .WithMessage("The 'datacontenttype' attribute, if present, must be a valid media type");
        this.RuleFor(e => e.DataSchema)
            .Must(dataSchema => dataSchema!.IsAbsoluteUri)
            .When(e => e.DataSchema != null)
            .WithMessage("The 'dataschema' attribute, if present, must be an absolute URI");
        this.RuleForEach(e => e.ExtensionAttributes!.Keys)
            .Must(name => AttributeNamePattern().IsMatch(name))
            .When(e => e.ExtensionAttributes != null)
            .WithName("extensions")
            .WithMessage((_, name) => $"The name of the extension attribute '{name}' is invalid: attribute names must only contain lower-case letters (a-z) and digits (0-9)");
    }

    /// <summary>
    /// Gets the pattern the names of context attributes must match
    /// </summary>
    [GeneratedRegex("^[a-z0-9]+$")]
    private static partial Regex AttributeNamePattern();

}
