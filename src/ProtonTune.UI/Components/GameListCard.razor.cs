using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Steam;

namespace ProtonTune.UI.Components;

/// <summary>
/// A library entry as a compact row, which fits more games on screen and lines their metadata up
/// into scannable columns.
/// </summary>
/// <remarks>Renders the list item itself, so the list container only has to lay out its children.</remarks>
public partial class GameListCard : ComponentBase
{
    /// <summary>The entry to show.</summary>
    [Parameter]
    [EditorRequired]
    public required SteamLibraryEntry Entry { get; set; }
}
