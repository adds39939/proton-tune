namespace ProtonTune.Core.Proton;

/// <summary>
/// Which Proton build a game runs under, as far as the files on disk can say.
/// </summary>
public sealed record ProtonSelection
{
    /// <summary>
    /// Whether the app has a mapping of its own. When <see langword="false"/> the rest of this
    /// record describes the default instead, which Steam applies only if the game's own metadata
    /// does not name a tool — so an inherited selection is the likely answer, not a certain one.
    /// </summary>
    public required bool IsExplicit { get; init; }

    /// <summary>
    /// The tool's internal name, or <see langword="null"/> when nothing is mapped and no default
    /// is set.
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// The installed build the name resolves to, or <see langword="null"/> when the named tool is
    /// not installed — an ordinary state after a Proton build is deleted but its mappings remain.
    /// </summary>
    public ProtonBuild? Build { get; init; }

    /// <summary>Whether a tool is named but cannot be found on disk.</summary>
    public bool IsMissing => ToolName is not null && Build is null;
}
