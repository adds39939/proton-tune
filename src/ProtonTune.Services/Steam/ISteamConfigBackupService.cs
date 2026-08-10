namespace ProtonTune.Services.Steam;

/// <summary>
/// Finds, prunes and restores the copies ProtonTune takes of Steam's configuration files.
/// </summary>
public interface ISteamConfigBackupService
{
    /// <summary>
    /// Every backup ProtonTune has taken, newest first.
    /// </summary>
    Task<IReadOnlyList<SteamConfigBackup>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the oldest backups of each file, keeping the newest few.
    /// </summary>
    /// <remarks>
    /// Counted per file rather than across all of them, or a busy day of editing launch options
    /// would push every copy of the installation's own configuration out of reach.
    /// </remarks>
    /// <returns>How many were removed.</returns>
    Task<int> PruneAsync(int keep, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a backup back, with Steam closed so the restored file survives.
    /// </summary>
    /// <remarks>
    /// The file being replaced is itself backed up first, so restoring the wrong one is no more
    /// final than the save that prompted it.
    /// </remarks>
    Task<SteamConfigRestoreResult> RestoreAsync(
        SteamConfigBackup backup,
        CancellationToken cancellationToken = default);
}

/// <summary>How a restore ended.</summary>
/// <param name="IsSuccess">Whether the file was put back.</param>
/// <param name="Message">Detail worth showing, when there is any.</param>
public sealed record SteamConfigRestoreResult(bool IsSuccess, string? Message = null)
{
    /// <summary>Whether Steam was closed and started again to make the restore stick.</summary>
    public bool SteamWasRestarted { get; init; }

    /// <summary>Where the replaced file was kept, so the restore itself can be undone.</summary>
    public string? ReplacedPath { get; init; }

    /// <summary>
    /// How many games stopped following the global profile because the restored options no longer
    /// match it.
    /// </summary>
    public int UnlinkedFromProfile { get; init; }
}
