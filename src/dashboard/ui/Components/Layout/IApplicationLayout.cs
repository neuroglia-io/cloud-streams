using System.ComponentModel;

namespace CloudStreams.Dashboard.Components;

/// <summary>
/// Defines the fundamentals of a service used to manage the application's layout
/// </summary>
public interface IApplicationLayout
    : INotifyPropertyChanged
{

    /// <summary>
    /// Gets the application's current title, which is aggregated to produce the current page's title
    /// </summary>
    ApplicationTitle? Title { get; set; }

    /// <summary>
    /// Handles changes in the application's title
    /// </summary>
    void OnTitleChanged();

}
