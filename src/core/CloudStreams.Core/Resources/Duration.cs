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

using CloudStreams.Core.Serialization;
using System.Globalization;
using System.Xml;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents a duration
/// </summary>
/// <remarks>
/// Durations are represented as <see href="https://en.wikipedia.org/wiki/ISO_8601#Durations">ISO 8601 durations</see> (e.g. <c>PT30S</c>), which is what the
/// resource definitions describe. The object form (e.g. <c>{ "seconds": 30 }</c>) produced by earlier versions is still accepted (see #168).
/// YAML serialization is implemented through <see cref="IYamlConvertible"/>, which YamlDotNet honours without requiring a converter to be registered
/// </remarks>
[DataContract, JsonConverter(typeof(DurationJsonConverter))]
public record Duration
    : IYamlConvertible
{

    /// <summary>
    /// Gets/sets the number of days
    /// </summary>
    [DataMember(Order = 1, Name = "days"), JsonPropertyName("days"), YamlMember(Alias = "days")]
    public virtual uint? Days { get; set; }

    /// <summary>
    /// Gets/sets the number of hours
    /// </summary>
    [DataMember(Order = 2, Name = "hours"), JsonPropertyName("hours"), YamlMember(Alias = "hours")]
    public virtual uint? Hours { get; set; }

    /// <summary>
    /// Gets/sets the number of minutes
    /// </summary>
    [DataMember(Order = 3, Name = "minutes"), JsonPropertyName("minutes"), YamlMember(Alias = "minutes")]
    public virtual uint? Minutes { get; set; }

    /// <summary>
    /// Gets/sets the number of seconds
    /// </summary>
    [DataMember(Order = 4, Name = "seconds"), JsonPropertyName("seconds"), YamlMember(Alias = "seconds")]
    public virtual uint? Seconds { get; set; }

    /// <summary>
    /// Gets/sets the number of milliseconds
    /// </summary>
    [DataMember(Order = 5, Name = "milliseconds"), JsonPropertyName("milliseconds"), YamlMember(Alias = "milliseconds")]
    public virtual uint? Milliseconds { get; set; }

    /// <summary>
    /// Converts the <see cref="Duration"/> into a new <see cref="TimeSpan"/>
    /// </summary>
    /// <returns>A new <see cref="TimeSpan"/></returns>
    /// <remarks>Components are added up without overflowing: a duration that exceeds <see cref="TimeSpan.MaxValue"/> is clamped to it (see #169)</remarks>
    public virtual TimeSpan ToTimeSpan()
    {
        var totalMilliseconds = (decimal)(this.Days ?? 0) * 86_400_000m
            + (decimal)(this.Hours ?? 0) * 3_600_000m
            + (decimal)(this.Minutes ?? 0) * 60_000m
            + (decimal)(this.Seconds ?? 0) * 1_000m
            + (this.Milliseconds ?? 0);
        var maxMilliseconds = (decimal)TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond;
        return totalMilliseconds >= maxMilliseconds ? TimeSpan.MaxValue : TimeSpan.FromTicks((long)(totalMilliseconds * TimeSpan.TicksPerMillisecond));
    }

    /// <summary>
    /// Formats the <see cref="Duration"/> as an ISO 8601 duration
    /// </summary>
    /// <returns>The ISO 8601 representation of the <see cref="Duration"/></returns>
    public override string ToString() => XmlConvert.ToString(this.ToTimeSpan());

    void IYamlConvertible.Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            if (!TryParse(scalar.Value, out var parsed)) throw new YamlException(scalar.Start, scalar.End, $"The value '{scalar.Value}' is not a valid, non-negative ISO 8601 duration, such as 'PT30S' or 'P1DT2H'");
            (this.Days, this.Hours, this.Minutes, this.Seconds, this.Milliseconds) = (parsed.Days, parsed.Hours, parsed.Minutes, parsed.Seconds, parsed.Milliseconds);
            return;
        }
        // the object form produced by earlier versions
        parser.Consume<MappingStart>();
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>();
            var value = parser.Consume<Scalar>();
            uint? component = string.IsNullOrEmpty(value.Value) ? null : uint.Parse(value.Value, CultureInfo.InvariantCulture);
            switch (key.Value.ToLowerInvariant())
            {
                case "days": this.Days = component; break;
                case "hours": this.Hours = component; break;
                case "minutes": this.Minutes = component; break;
                case "seconds": this.Seconds = component; break;
                case "milliseconds": this.Milliseconds = component; break;
                default: throw new YamlException(key.Start, key.End, $"Unknown duration component '{key.Value}'");
            }
        }
    }

    void IYamlConvertible.Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer) => emitter.Emit(new Scalar(this.ToString()));

    /// <summary>
    /// Gets a zero <see cref="Duration"/>
    /// </summary>
    public static readonly Duration Zero = new();

    /// <summary>
    /// Creates a new <see cref="Duration"/> from the specified amount of days
    /// </summary>
    /// <param name="days">The amount of days</param>
    /// <returns>A new <see cref="Duration"/></returns>
    public static Duration FromDays(uint days) => new() { Days = days };

    /// <summary>
    /// Creates a new <see cref="Duration"/> from the specified amount of hours
    /// </summary>
    /// <param name="hours">The amount of hours</param>
    /// <returns>A new <see cref="Duration"/></returns>
    public static Duration FromHours(uint hours) => new() { Hours = hours };

    /// <summary>
    /// Creates a new <see cref="Duration"/> from the specified amount of minutes
    /// </summary>
    /// <param name="minutes">The amount of minutes</param>
    /// <returns>A new <see cref="Duration"/></returns>
    public static Duration FromMinutes(uint minutes) => new() { Minutes = minutes };

    /// <summary>
    /// Creates a new <see cref="Duration"/> from the specified amount of seconds
    /// </summary>
    /// <param name="seconds">The amount of seconds</param>
    /// <returns>A new <see cref="Duration"/></returns>
    public static Duration FromSeconds(uint seconds) => new() { Seconds = seconds };

    /// <summary>
    /// Creates a new <see cref="Duration"/> from the specified amount of milliseconds
    /// </summary>
    /// <param name="milliseconds">The amount of milliseconds</param>
    /// <returns>A new <see cref="Duration"/></returns>
    public static Duration FromMilliseconds(uint milliseconds) => new() { Milliseconds = milliseconds };

    /// <summary>
    /// Creates a new <see cref="Duration"/> from the specified <see cref="TimeSpan"/>
    /// </summary>
    /// <param name="timeSpan">The <see cref="TimeSpan"/> to create a new <see cref="Duration"/> from</param>
    /// <returns>A new <see cref="Duration"/></returns>
    /// <exception cref="ArgumentOutOfRangeException">The specified <see cref="TimeSpan"/> is negative</exception>
    public static Duration FromTimeSpan(TimeSpan timeSpan)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeSpan, TimeSpan.Zero);
        return new()
        {
            Days = (uint)timeSpan.Days,
            Hours = (uint)timeSpan.Hours,
            Minutes = (uint)timeSpan.Minutes,
            Seconds = (uint)timeSpan.Seconds,
            Milliseconds = (uint)timeSpan.Milliseconds
        };
    }

    /// <summary>
    /// Parses the specified ISO 8601 duration
    /// </summary>
    /// <param name="value">The ISO 8601 duration to parse</param>
    /// <returns>The parsed <see cref="Duration"/></returns>
    /// <exception cref="FormatException">The specified value is not a valid, non-negative ISO 8601 duration</exception>
    public static Duration Parse(string value)
    {
        if (!TryParse(value, out var duration)) throw new FormatException($"The value '{value}' is not a valid, non-negative ISO 8601 duration, such as 'PT30S' or 'P1DT2H'");
        return duration;
    }

    /// <summary>
    /// Attempts to parse the specified ISO 8601 duration
    /// </summary>
    /// <param name="value">The ISO 8601 duration to parse</param>
    /// <param name="duration">The parsed <see cref="Duration"/>, if any</param>
    /// <returns>A boolean indicating whether or not the specified value could be parsed</returns>
    public static bool TryParse(string? value, out Duration duration)
    {
        duration = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;
        TimeSpan timeSpan;
        try { timeSpan = XmlConvert.ToTimeSpan(value.Trim()); }
        catch (Exception ex) when (ex is FormatException or OverflowException) { return false; }
        if (timeSpan < TimeSpan.Zero) return false;
        duration = FromTimeSpan(timeSpan);
        return true;
    }

    /// <summary>
    /// Implicitly converts the specified <see cref="Duration"/> into a new <see cref="TimeSpan"/>
    /// </summary>
    /// <param name="duration">The <see cref="Duration"/> to convert</param>
    public static implicit operator TimeSpan?(Duration? duration) => duration?.ToTimeSpan();

    /// <summary>
    /// Implicitly converts the specified <see cref="TimeSpan"/> into a new <see cref="Duration"/>
    /// </summary>
    /// <param name="timeSpan">The <see cref="TimeSpan"/> to convert</param>
    public static implicit operator Duration?(TimeSpan? timeSpan) => timeSpan == null ? null : FromTimeSpan(timeSpan.Value);

}
