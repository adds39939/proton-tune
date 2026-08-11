namespace ProtonTune.Core.Settings;

/// <summary>
/// How the library orders its entries.
/// </summary>
/// <remarks>
/// Declared here rather than beside the component because it is remembered between sessions, and
/// <see cref="AppSettings" /> cannot reach into the UI. The order is the order the menu lists
/// them in, and the first is what a fresh installation opens on.
/// </remarks>
public enum LibrarySortOrder
{
    /// <summary>Alphabetical, which is the order to reach for when looking something up.</summary>
    Name,

    /// <summary>
    /// Most recently played first, which puts the games worth configuring at the top. Games never
    /// played come last rather than first: no timestamp is not the same as a very old one.
    /// </summary>
    RecentlyPlayed
}
