using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Steam;

namespace ProtonTune.UI.Components.Game;

/// <summary>
/// The badges that qualify a library entry. Only one for now — whether the install actually
/// finished — so it renders nothing at all in the common case.
/// </summary>
public partial class GameTags : ComponentBase
{
    /// <summary>The entry to describe.</summary>
    [Parameter]
    [EditorRequired]
    public required SteamLibraryEntry Entry { get; set; }
}
