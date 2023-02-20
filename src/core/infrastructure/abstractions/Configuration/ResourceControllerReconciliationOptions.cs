using CloudStreams.Core.Infrastructure.Services;

namespace CloudStreams.Core.Infrastructure.Configuration;

/// <summary>
/// Represents the options used to configure the reconciliation loop of a <see cref="ResourceController{TResource}"/>
/// </summary>
public class ResourceControllerReconciliationOptions
{

    /// <summary>
    /// Gets/sets the interval at which to perform resource reconciliation
    /// </summary>
    public virtual TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

}