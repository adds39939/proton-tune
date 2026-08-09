using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Steam;

namespace ProtonTune.UI.Components.Library;

/// <summary>
/// A library entry as a cover-art tile, the way Steam presents a library.
/// </summary>
/// <remarks>Renders the list item itself, so the grid container only has to lay out its children.</remarks>
public partial class GameGridCard : ComponentBase
{
    /// <summary>The entry to show.</summary>
    [Parameter]
    [EditorRequired]
    public required SteamLibraryEntry Entry { get; set; }

    /// <summary>Raised when the card is chosen for configuration.</summary>
    [Parameter]
    public EventCallback<SteamLibraryEntry> OnSelect { get; set; }

    private Task Select() => OnSelect.InvokeAsync(Entry);
}
