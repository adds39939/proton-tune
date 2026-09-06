using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Steam;

namespace ProtonTune.UI.Components.Library;

/// <summary>
/// A library entry as a compact row, with its metadata in fixed columns that line up down the
/// list.
/// </summary>
/// <remarks>Renders the list item itself, so the list container only has to lay out its children.</remarks>
public partial class GameListCard : ComponentBase
{
    /// <summary>The entry to show.</summary>
    [Parameter]
    [EditorRequired]
    public required SteamLibraryEntry Entry { get; set; }

    /// <summary>Raised when the row is chosen for configuration.</summary>
    [Parameter]
    public EventCallback<SteamLibraryEntry> OnSelect { get; set; }

    private Task Select() => OnSelect.InvokeAsync(Entry);
}
