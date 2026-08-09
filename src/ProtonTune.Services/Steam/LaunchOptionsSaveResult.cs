namespace ProtonTune.Services.Steam;

/// <summary>How a save attempt ended.</summary>
public enum LaunchOptionsSaveStatus
{
    /// <summary>The value was written and read back successfully.</summary>
    Saved,

    /// <summary>A game is running. Closing Steam would end that session, so nothing was done.</summary>
    GameRunning,

    /// <summary>Steam was asked to close but had not exited in time. Nothing was written.</summary>
    SteamStillRunning,

    /// <summary>No Steam user configuration could be found to write to.</summary>
    NoUserConfig,

    /// <summary>The configuration file was not the document it was expected to be.</summary>
    ConfigUnrecognised,

    /// <summary>The write itself failed, or what was read back afterwards did not match.</summary>
    WriteFailed
}

/// <summary>
/// The outcome of writing launch options, including what had to be done to Steam along the way.
/// </summary>
/// <param name="Status">How it ended.</param>
/// <param name="Message">Detail worth showing the user, when there is any.</param>
public sealed record LaunchOptionsSaveResult(LaunchOptionsSaveStatus Status, string? Message = null)
{
    /// <summary>Whether the value was actually written.</summary>
    public bool IsSuccess => Status == LaunchOptionsSaveStatus.Saved;

    /// <summary>Whether Steam was shut down and started again to make the write stick.</summary>
    public bool SteamWasRestarted { get; init; }

    /// <summary>Where the previous configuration was copied before anything was changed.</summary>
    public string? BackupPath { get; init; }
}
