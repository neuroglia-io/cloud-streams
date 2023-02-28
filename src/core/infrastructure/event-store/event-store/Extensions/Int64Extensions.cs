namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="long"/>s
/// </summary>
public static class Int64Extensions
{

    /// <summary>
    /// Converts an <see cref="long"/> into a new <see cref="FromStream"/> value
    /// </summary>
    /// <param name="value">The value to convert</param>
    /// <returns>A new <see cref="FromStream"/> value</returns>
    public static FromStream ToSubscriptionPosition(this long value)
    {
        return value switch
        {
            StreamPosition.StartOfStream => FromStream.Start,
            StreamPosition.EndOfStream => FromStream.End,
            _ => FromStream.After(EventStore.Client.StreamPosition.FromInt64(value))
        };
    }

}
