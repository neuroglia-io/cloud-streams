using CloudStreams.Core.Resources;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="RetryBackoffDuration"/>s
/// </summary>
public static class RetryBackoffDurationExtensions
{

    /// <summary>
    /// Computes the backoff duration for the specified retry attempt
    /// </summary>
    /// <param name="duration">The extended <see cref="RetryBackoffDuration"/></param>
    /// <param name="attemptNumber">The number of the retry attempt to get the backoff duration for</param>
    /// <returns>A new <see cref="TimeSpan"/> that represents the backoff duration for the specified retry attempt</returns>
    public static TimeSpan ForAttempt(this RetryBackoffDuration duration, int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(duration);
        var timeSpan = duration.Period.ToTimeSpan();
        return duration.Type switch
        {
            RetryBackoffDurationType.Constant => timeSpan,
            RetryBackoffDurationType.Exponential => Math.Pow(attemptNumber, duration.Exponent!.Value) * timeSpan,
            RetryBackoffDurationType.Incremental => attemptNumber * timeSpan,
            _ => throw new NotSupportedException($"The specified {nameof(RetryBackoffDurationType)} '{duration.Type}' is not supported")
        };
    }

}