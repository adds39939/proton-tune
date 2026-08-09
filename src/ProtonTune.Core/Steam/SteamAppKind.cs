namespace ProtonTune.Core.Steam;

/// <summary>
/// Distinguishes playable titles from the support software Steam installs alongside them.
/// </summary>
public enum SteamAppKind
{
    /// <summary>A playable game.</summary>
    Game,

    /// <summary>
    /// A compatibility tool or runtime — Proton, the Steam Linux Runtime, redistributables.
    /// These share the library with games but are never launched directly.
    /// </summary>
    Tool
}
