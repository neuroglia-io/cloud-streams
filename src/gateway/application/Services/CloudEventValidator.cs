// Copyright © 2023-Present The Cloud Streams Authors
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