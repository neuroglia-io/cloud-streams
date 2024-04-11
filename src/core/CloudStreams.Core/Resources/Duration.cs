namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure a duration
/// </summary>
[DataContract]
public record Duration
{

    /// <summary>
    /// Gets/sets the numbers of days, if any
    /// </summary>
    public virtual uint? Days { get; set; }

    /// <summary>
    /// Gets/sets the numbers of hours, if any
    /// </summary>
    public virtual uint? Hours { get; set; }

    /// <summary>
    /// Gets/sets the numbers of minutes, if any
    /// </summary>
    public virtual uint? Minutes { get; set; }

    /// <summary>
    /// Gets/sets the numbers of seconds, if any
    /// </summary>
    public virtual uint? Seconds { get; set; }

    /// <summary>
    /// Gets/sets the numbers of milliseconds, if any
    /// </summary>
    public virtual uint? Milliseconds { get; set; }

    /// <summary>
    /// Converts the <see cref="Duration"/> to a new <see cref="TimeSpan"/>
    /// </summary>
    /// <returns>A new <see cref="TimeSpan"/></returns>
    public virtual TimeSpan ToTimeSpan() => new((int)(this.Days ?? 0), (int)(this.Hours ?? 0), (int)(this.Minutes ?? 0), (int)(this.Seconds ?? 0), (int)(this.Milliseconds ?? 0));

    /// <summary>
    /// Gets a zero <see cref="Duration"/> value
    /// </summary>
    public static readonly Duration Zero = new();

    /// <summary>
    /// Creates a new <see cref="Duration"/> object representing the specified number of days.
    /// </summary>
    /// <param name="days">The number of days.</param>
    /// <returns>A new <see cref="Duration"/> object with the specified number of days.</returns>
    public static Duration FromDays(uint days) => new() { Days = days };

    /// <summary>
    /// Creates a new <see cref="Duration"/> object representing the specified number of hours.
    /// </summary>
    /// <param name="hours">The number of hours.</param>
    /// <returns>A new <see cref="Duration"/> object with the specified number of hours.</returns>
    public static Duration FromHours(uint hours) => new() { Hours = hours };

    /// <summary>
    /// Creates a new <see cref="Duration"/> object representing the specified number of minutes.
    /// </summary>
    /// <param name="minutes">The number of minutes.</param>
    /// <returns>A new <see cref="Duration"/> object with the specified number of minutes.</returns>
    public static Duration FromMinutes(uint minutes) => new() { Minutes = minutes };

    /// <summary>
    /// Creates a new <see cref="Duration"/> object representing the specified number of seconds.
    /// </summary>
    /// <param name="seconds">The number of seconds.</param>
    /// <returns>A new <see cref="Duration"/> object with the specified number of seconds.</returns>
    public static Duration FromSeconds(uint seconds) => new() { Seconds = seconds };

    /// <summary>
    /// Creates a new <see cref="Duration"/> object representing the specified number of milliseconds.
    /// </summary>
    /// <param name="milliseconds">The number of milliseconds.</param>
    /// <returns>A new <see cref="Duration"/> object with the specified number of milliseconds.</returns>
    public static Duration FromMilliseconds(uint milliseconds) => new() { Milliseconds = milliseconds };

    /// <summary>
    /// Creates a new <see cref="Duration"/> representing the specified <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="timeSpan">The <see cref="TimeSpan"/> to convert.</param>
    /// <returns>A new <see cref="Duration"/> representing the specified <see cref="TimeSpan"/>.</returns>
    public static Duration FromTimeSpan(TimeSpan timeSpan) => new()
    {
        Days = (uint)timeSpan.Days,
        Hours = (uint)timeSpan.Hours,
        Minutes = (uint)timeSpan.Minutes,
        Seconds = (uint)timeSpan.Seconds,
        Milliseconds = (uint)timeSpan.Milliseconds
    };

    /// <summary>
    /// Converts the specified <see cref="Duration"/> into a new <see cref="TimeSpan"/>
    /// </summary>
    /// <param name="duration">The <see cref="Duration"/> to convert</param>
    public static implicit operator TimeSpan?(Duration? duration) => duration?.ToTimeSpan();

    /// <summary>
    /// Converts the specified <see cref="TimeSpan"/> into a new <see cref="Duration"/>
    /// </summary>
    /// <param name="timeSpan">The <see cref="TimeSpan"/> to convert</param>
    public static implicit operator Duration?(TimeSpan? timeSpan) => timeSpan == null ? null : FromTimeSpan(timeSpan.Value);

}