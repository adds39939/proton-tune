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
    /// The same settings with the retention held inside what is allowed, so a hand-edited file
    /// cannot ask for none or for thousands.
    /// </summary>
    public AppSettings Sanitised() => this with
    {
        BackupsToKeep = Math.Clamp(BackupsToKeep, MinimumBackupsToKeep, MaximumBackupsToKeep)
    };
}
