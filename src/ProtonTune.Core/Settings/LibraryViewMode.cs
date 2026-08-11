namespace ProtonTune.Core.Settings;

/// <summary>
/// How the library presents its entries.
/// </summary>
/// <remarks>
/// Declared here rather than beside the component because it is remembered between sessions, and
/// <see cref="AppSettings" /> cannot reach into the UI. The order is the order the buttons appear
/// in, and the first is what a fresh installation opens on.
/// </remarks>
public enum LibraryViewMode
{
    /// <summary>Compact rows, which fit more games on screen and read better when scanning.</summary>
    List,

    /// <summary>Cover art in a grid, the way Steam shows a library.</summary>
    Grid
}
