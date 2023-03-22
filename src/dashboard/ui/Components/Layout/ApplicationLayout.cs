using Microsoft.AspNetCore.Components;
using System.ComponentModel;

namespace CloudStreams.Dashboard.Components;

/// <summary>
/// Represents the default implementation of the <see cref="IApplicationLayout"/> interface
/// </summary>
public class ApplicationLayout
    : IApplicationLayout
{

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the application title's <see cref="RenderFragment"/>
    /// </summary>
    public RenderFragment? TitleFragment => this.Title?.ChildContent;

    private ApplicationTitle? _Title;
    /// <summary>
    /// Gets/sets the application's title
    /// </summary>
    public ApplicationTitle? Title
    {
        get => this._Title;
        set
        {
            if (this._Title == value) return;
            this._Title = value;
            this.OnTitleChanged();
        }
    }

    /// <inheritdoc/>
    public void OnTitleChanged()
    {
        if (this.PropertyChanged != null) this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Title)));
    }

}
