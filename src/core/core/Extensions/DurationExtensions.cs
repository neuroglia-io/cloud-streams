namespace CloudStreams;

/// <summary>
/// Defines extensions for <see cref="Duration"/>s
/// </summary>
public static class DurationExtensions
{

    /// <summary>
    /// Converts the <see cref="Duration"/> into a <see cref="TimeSpan"/>
    /// </summary>
    /// <param name="duration">The <see cref="Duration"/> to convert</param>
    /// <returns>The converted <see cref="TimeSpan"/></returns>
    public static TimeSpan ToTimeSpan(this Duration duration)
    {
        int days = (int)duration.Days;
        days += (int)duration.Weeks * 7;
        days += (int)duration.Months * 30;
        days += (int)duration.Years * 365;
        return new TimeSpan(days, (int)duration.Hours, (int)duration.Minutes, (int)duration.Seconds);
    }

}
