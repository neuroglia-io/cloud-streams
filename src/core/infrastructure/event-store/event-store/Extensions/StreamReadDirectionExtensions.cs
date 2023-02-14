namespace CloudStreams.Infrastructure;

/// <summary>
/// Defines extensions for <see cref="StreamReadDirection"/>s
/// </summary>
public static class StreamReadDirectionExtensions
{

    /// <summary>
    /// Converts the <see cref="StreamReadDirection"/> into a <see cref="Direction"/>
    /// </summary>
    /// <param name="readDirection">The <see cref="StreamReadDirection"/> to convert</param>
    /// <returns>The converted <see cref="Direction"/></returns>
    public static Direction ToDirection(this StreamReadDirection readDirection)
    {
        return readDirection switch
        {
            StreamReadDirection.Forwards => Direction.Forwards,
            StreamReadDirection.Backwards => Direction.Backwards,
            _ => throw new NotSupportedException($"The specified stream read direction '{readDirection}' is not supported")
        };
    }

}
