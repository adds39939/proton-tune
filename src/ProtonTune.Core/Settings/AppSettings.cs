namespace ProtonTune.Core.Settings;

/// <summary>
/// What ProtonTune remembers about how it should behave, as opposed to what it does to a game.
/// </summary>
public sealed record AppSettings
{
    /// <summary>How many backups of each Steam configuration file to keep by default.</summary>
    /// <remarks>Every save makes one, so this bounds a directory of 130 KB files.</remarks>
    public const int DefaultBackupsToKeep = 10;

    /// <summary>The fewest that may be kept. Keeping none would make a bad save unrecoverable.</summary>
    public const int MinimumBackupsToKeep = 1;

    public const int MaximumBackupsToKeep = 100;

    /// <summary>
    /// How many backups of each Steam configuration file are kept. The oldest are removed once
    /// there are more than this.
    /// </summary>
    public int BackupsToKeep { get; init; } = DefaultBackupsToKeep;

    /// <summary>
    /// Whether the library was last left showing rows or cover art.
    /// </summary>
    public LibraryViewMode LibraryView { get; init; }

    /// <summary>The order the library was last left in.</summary>
    public LibrarySortOrder LibrarySort { get; init; }

    /// <summary>
    /// The same settings clamped to what is allowed, so a hand-edited file cannot ask for no
    /// backups, thousands of them, or a view that does not exist.
    /// </summary>
    public AppSettings Sanitised() => this with
    {
        BackupsToKeep = Math.Clamp(BackupsToKeep, MinimumBackupsToKeep, MaximumBackupsToKeep),
        LibraryView = Enum.IsDefined(LibraryView) ? LibraryView : default,
        LibrarySort = Enum.IsDefined(LibrarySort) ? LibrarySort : default
    };
}
