using CloudStreams.Core.Data.Models;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="Subscription"/>s
/// </summary>
public static class SubscriptionExtensions
{

    /// <summary>
    /// Gets the <see cref="Subscription"/>'s offset
    /// </summary>
    /// <param name="subscription">The <see cref="Subscription"/> to get the offset of</param>
    /// <returns>The <see cref="Subscription"/>'s offset</returns>
    public static long GetOffset(this Subscription subscription)
    {
        if (subscription.Status?.Stream?.AckedOffset.HasValue == true && subscription.Status.ObservedGeneration == subscription.Metadata.Generation) return (long)subscription.Status.Stream.AckedOffset.Value;
        if (subscription.Spec.Stream?.Offset.HasValue == true) return subscription.Spec.Stream.Offset.Value;
        return StreamPosition.EndOfStream;
    }

}
