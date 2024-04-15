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

namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure cloud event validation
/// </summary>
[DataContract]
public record CloudEventValidationPolicy
{

    /// <summary>
    /// Gets/sets a boolean indicating whether or not to skip cloud event validation
    /// </summary>
    [DataMember(Order = 1, Name = "skip"), JsonPropertyName("skip"), YamlMember(Alias = "skip")]
    public virtual bool SkipValidation { get; set; }

    /// <summary>
    /// Gets/sets the strategy to use when validating inbound cloud events<para></para>
    /// Defaults to '<see cref="CloudEventValidationStrategy.Fail"/>'
    /// </summary>
    [DefaultValue(CloudEventValidationStrategy.Fail)]
    [DataMember(Order = 2, Name = "validationStrategy"), JsonPropertyName("validationStrategy"), YamlMember(Alias = "validationStrategy")]
    public virtual string ValidationStrategy { get; set; } = CloudEventValidationStrategy.Fail;

    /// <summary>
    /// Gets/sets an object used to configure the JSON schema based validation of incoming cloud events
    /// </summary>
    [DataMember(Order = 3, Name = "dataSchema"), JsonPropertyName("dataSchema"), YamlMember(Alias = "dataSchema")]
    public virtual DataSchemaValidationPolicy? DataSchema { get; set; }

}
