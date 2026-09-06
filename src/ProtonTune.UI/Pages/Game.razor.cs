using Microsoft.AspNetCore.Components;

namespace ProtonTune.UI.Pages;

/// <summary>
/// One game's configuration, addressed by its Steam app identifier.
/// </summary>
/// <remarks>
/// A page rather than a dialog over the library, so the game being configured is somewhere the
/// address bar can name and the back gesture can leave.
/// </remarks>
public partial class Game : ComponentBase
{
    /// <summary>
    /// The Steam app identifier from the route. Steam's are unsigned, and <c>long</c> is the
    /// widest whole number a route constraint offers; the panel is what decides whether one names
    /// an installed game.
    /// </summary>
    [Parameter]
    public long AppId { get; set; }
}
