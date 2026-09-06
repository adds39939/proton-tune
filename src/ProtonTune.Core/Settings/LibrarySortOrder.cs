namespace ProtonTune.Core.Settings;

/// <summary>
/// How the library orders its entries.
/// </summary>
/// <remarks>
/// Declared here rather than beside the component because <see cref="AppSettings" /> remembers it
/// and cannot reach into the UI. Declaration order is menu order; the first is the default.
/// </remarks>
public enum LibrarySortOrder
{
    /// <summary>Alphabetical.</summary>
    Name,

    /// <summary>
    /// Most recently played first. Games never played come last rather than first: no timestamp is
    /// not the same as a very old one.
    /// </summary>
    RecentlyPlayed
}
