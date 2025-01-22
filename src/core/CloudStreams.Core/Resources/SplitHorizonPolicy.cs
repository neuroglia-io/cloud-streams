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
/// Represents an object used to configure a cloud event gateway's split horizon policy
/// </summary>
[DataContract]
public record SplitHorizonPolicy
{

    /// <summary>
    /// Gets the default name of the context attribute used to carry information about the cloud event's origin
    /// </summary>
    public const string DefaultAttributeName = "origin";

    /// <summary>
    /// Gets/sets the name of the context attribute used to carry information about the cloud event's origin
    /// </summary>
    [DataMember(Order = 1, Name = "originAttribute"), JsonPropertyOrder(1), JsonPropertyName("originAttribute"), YamlMember(Order = 1, Alias = "originAttribute")]
    public virtual string OriginAttribute { get; set; } = DefaultAttributeName;

    /// <summary>
    /// Gets/sets the horizon tracking mode, which defines whether to keep a short or detailed record of event origins
    /// </summary>
    [DataMember(Order = 2, Name = "trackingMode"), JsonPropertyOrder(2), JsonPropertyName("trackingMode"), YamlMember(Order = 2, Alias = "trackingMode")]
    public virtual HorizonTrackingMode TrackingMode { get; set; } = HorizonTrackingMode.Single;

    /// <summary>
    /// Gets/sets the horizon tracking mode, which defines whether to keep a short or detailed record of event origins
    /// </summary>
    [DataMember(Order = 3, Name = "evaluationMode"), JsonPropertyOrder(3), JsonPropertyName("evaluationMode"), YamlMember(Order = 3, Alias = "evaluationMode")]
    public virtual HorizonEvaluationMode EvaluationMode { get; set; } = HorizonEvaluationMode.Last;

    /// <summary>
    /// Gets/sets the action to undertake when a loop is detected
    /// </summary>
    [DataMember(Order = 4, Name = "loopAction"), JsonPropertyOrder(4), JsonPropertyName("loopAction"), YamlMember(Order = 4, Alias = "loopAction")]
    public virtual LoopAction LoopAction { get; set; } = LoopAction.Ignore;

    /// <summary>
    /// Gets/sets a limit to how many gateways can be listed before ignoring or rejecting the event as potentially looping.<para></para>Ignored if <see cref="TrackingMode"/> has not been set to <see cref="HorizonTrackingMode.Chain"/>.
    /// </summary>
    [DataMember(Order = 5, Name = "maxChainLength"), JsonPropertyOrder(5), JsonPropertyName("maxChainLength"), YamlMember(Order = 5, Alias = "maxChainLength")]
    public virtual uint? MaxChainLength { get; set; }

    /// <summary>
    /// Gets/sets a list, if any, of gateways to explicitly allow events from 
    /// </summary>
    [DataMember(Order = 6, Name = "allowedOrigins"), JsonPropertyOrder(6), JsonPropertyName("allowedOrigins"), YamlMember(Order = 6, Alias = "allowedOrigins")]
    public virtual List<string>? AllowedOrigins { get; set; }

    /// <summary>
    /// Gets/sets a list, if any, of gateways to explicitly reject events from 
    /// </summary>
    [DataMember(Order = 7, Name = "excludedOrigins"), JsonPropertyOrder(7), JsonPropertyName("excludedOrigins"), YamlMember(Order = 7, Alias = "excludedOrigins")]
    public virtual List<string>? ExcludedOrigins { get; set; }

}