namespace ProtonTune.Core.Settings;

/// <summary>
/// What ProtonTune remembers about how it should behave, as opposed to what it does to a game.
/// </summary>
public sealed record AppSettings
{
    /// <summary>How many backups of each Steam configuration file to keep by default.</summary>
    /// <remarks>
    /// Enough to reach back past a few bad saves, few enough that a directory of 130 KB files
    /// does not grow without end. Every save makes one.
    /// </remarks>
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
    /// Whether the library was last left showing rows or cover art. Remembered because it is a
    /// standing preference about how someone reads a list, not a choice worth making twice.
    /// </summary>
    public LibraryViewMode LibraryView { get; init; }

    /// <summary>The order the library was last left in, remembered for the same reason.</summary>
    public LibrarySortOrder LibrarySort { get; init; }

    /// <summary>
    /// The same settings with everything held inside what is allowed, so a hand-edited file cannot
    /// ask for no backups, for thousands of them, or for a view that does not exist.
    /// </summary>
    public AppSettings Sanitised() => this with
    {
        BackupsToKeep = Math.Clamp(BackupsToKeep, MinimumBackupsToKeep, MaximumBackupsToKeep),
        LibraryView = Enum.IsDefined(LibraryView) ? LibraryView : default,
        LibrarySort = Enum.IsDefined(LibrarySort) ? LibrarySort : default
    };
}
