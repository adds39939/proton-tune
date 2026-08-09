namespace ProtonTune.Core.Steam;

/// <summary>
/// The shapes of store artwork Steam publishes for an app.
/// </summary>
public enum GameArtworkKind
{
    /// <summary>
    /// The portrait cover (600×900) Steam shows in its own library grid.
    /// </summary>
    Capsule,

    /// <summary>
    /// The wide header banner (460×215), which suits compact rows.
    /// </summary>
    Header
}
