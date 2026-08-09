using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Steam;

namespace ProtonTune.UI.Components.Game;

/// <summary>
/// The badges that qualify a library entry — whether it is a compatibility tool rather than a
/// game, and whether its install actually finished. Renders nothing when neither applies, which
/// is the common case.
/// </summary>
public partial class GameTags : ComponentBase
{
    /// <summary>The entry to describe.</summary>
    [Parameter]
    [EditorRequired]
    public required SteamLibraryEntry Entry { get; set; }
}
