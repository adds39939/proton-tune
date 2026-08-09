using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ProtonTune.Core.Steam;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Per-game configuration. A placeholder for now: it shows the shape of the launch options and
/// environment variables ProtonTune will eventually write, with the inputs disabled so nothing
/// suggests a setting is being saved.
/// </summary>
public partial class GameConfigDialog : ComponentBase
{
    /// <summary>The entry being configured.</summary>
    [Parameter]
    [EditorRequired]
    public required SteamLibraryEntry Entry { get; set; }

    /// <summary>Raised when the dialog asks to be dismissed.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    private Task Close() => OnClose.InvokeAsync();

    /// <summary>
    /// Dismisses on Escape. The close button takes focus when the dialog opens, so the key event
    /// starts inside the panel and bubbles to the handler.
    /// </summary>
    private Task OnKeyDown(KeyboardEventArgs args) =>
        args.Key == "Escape" ? Close() : Task.CompletedTask;
}
