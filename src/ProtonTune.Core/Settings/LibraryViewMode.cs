namespace ProtonTune.Core.Settings;

/// <summary>
/// How the library presents its entries.
/// </summary>
/// <remarks>
/// Declared here rather than beside the component because <see cref="AppSettings" /> remembers it
/// and cannot reach into the UI. Declaration order is button order; the first is the default.
/// </remarks>
public enum LibraryViewMode
{
    /// <summary>Compact rows, which fit more games on screen.</summary>
    List,

    /// <summary>Cover art in a grid, the way Steam shows a library.</summary>
    Grid
}
