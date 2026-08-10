namespace ProtonTune.Core.Proton;

/// <summary>
/// One entry from Steam's <c>CompatToolMapping</c> — a deliberate choice of compatibility tool
/// for an app, recorded in <c>config/config.vdf</c>.
/// </summary>
/// <remarks>
/// Only choices made in Steam's own interface appear here. Steam also maps tools onto games from
/// its app metadata, and those mappings live in a binary cache rather than this file, so a game
/// missing from the mapping is not a game running without Proton.
/// </remarks>
public sealed record ProtonToolMapping
{
    /// <summary>
    /// The app the mapping applies to, or <see cref="ProtonCatalogue.DefaultAppId" /> for the
    /// default that covers everything without a choice of its own.
    /// </summary>
    public required uint AppId { get; init; }

    /// <summary>The tool's internal name, matching <see cref="ProtonBuild.Name" />.</summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Extra configuration Steam attaches to the mapping. Empty in every case seen so far, and
    /// carried through unchanged rather than interpreted.
    /// </summary>
    public string Config { get; init; } = string.Empty;

    /// <summary>
    /// How strongly the mapping is held. Steam resolves competing mappings for one app by taking
    /// the highest: a choice made by hand outranks one that came from app metadata.
    /// </summary>
    public int Priority { get; init; }
}
