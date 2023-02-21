using Microsoft.AspNetCore.Components;

namespace CloudStreams.Dashboard.Components;


/// <summary>
/// Represents the base class of all modals in the Cloud Streams Dashboard
/// </summary>
public abstract class ModalBase
    : ComponentBase
{

    /// <summary>
    /// Gets/sets the modal's size. Defaults to <see cref="ModalSize.Large"/>
    /// </summary>
    [Parameter]
    public virtual ModalSize Size { get; set; } = ModalSize.Large;

    /// <summary>
    /// Gets/sets a boolean indicating whether or not to show the close icon
    /// </summary>
    [Parameter]
    public virtual bool ShowCloseIcon { get; set; } = true;

    /// <summary>
    /// Gets/sets an <see cref="EventCallback{TValue}"/> fired whenever the modal becomes active or inactive
    /// </summary>
    [Parameter]
    public virtual EventCallback<bool> OnActiveChange { get; set; }

    /// <summary>
    /// Gets/sets an <see cref="EventCallback"/> fired whenever the modal is being shown
    /// </summary>
    [Parameter]
    public virtual EventCallback OnShow { get; set; }

    /// <summary>
    /// Gets/sets an <see cref="EventCallback"/> fired whenever the modal is being hidden
    /// </summary>
    [Parameter]
    public virtual EventCallback OnHide { get; set; }

    /// <summary>
    /// Gets/sets a boolean indicating whether or not the modal is active
    /// </summary>
    public virtual bool Active { get; set; } = false;

    /// <summary>
    /// Gets the css class for the modal's size
    /// </summary>
    /// <returns>The css class for the modal's size</returns>
    public virtual string? GetModalSizeCssClass()
    {
        return this.Size switch
        {
            ModalSize.Small => "modal-sm",
            ModalSize.Default => "",
            ModalSize.Large => "modal-lg",
            ModalSize.ExtraLarge => "modal-xl",
            _ => throw new NotSupportedException($"The specified {nameof(ModalSize)} '{this.Size}' is not supported")
        };
    }

    /// <summary>
/// Toggles the modal's visibility
/// </summary>
/// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task ToggleAsync()
    {
        this.Active = !this.Active;
        await this.OnActiveChange.InvokeAsync(this.Active);
        if (!this.Active) await this.OnHide.InvokeAsync();
    }

    /// <summary>
    /// Shows the modal
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task ShowAsync()
    {
        this.Active = true;
        await this.OnActiveChange.InvokeAsync(this.Active);
        await this.OnShow.InvokeAsync();
    }

    /// <summary>
    /// Hides the modal
    /// </summary>
    /// <returns>A new awaitable <see cref="Task"/></returns>
    public virtual async Task HideAsync()
    {
        this.Active = false;
        await this.OnActiveChange.InvokeAsync(this.Active);
        await this.OnHide.InvokeAsync();
    }
}
