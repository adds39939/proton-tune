namespace ProtonTune.Services.Steam;

/// <summary>
/// Observes and controls the running Steam client.
/// </summary>
public interface ISteamClient
{
    /// <summary>Whether the Steam client is currently running.</summary>
    bool IsRunning();

    /// <summary>
    /// Whether Steam is currently running a game. Checked before anything else, since shutting
    /// Steam down would end that session mid-play.
    /// </summary>
    bool IsGameRunning();

    /// <summary>
    /// Asks Steam to close and waits for it to actually exit.
    /// </summary>
    /// <returns><see langword="true"/> once Steam has exited, false if it outlasted the timeout.</returns>
    /// <remarks>
    /// Waiting for the process to go is the point: Steam writes its configuration on the way out,
    /// so those files are safe to edit only once it has gone, not once the request was accepted.
    /// </remarks>
    Task<bool> ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>Starts the Steam client without waiting for it.</summary>
    bool Start();

    /// <summary>
    /// Asks Steam to run a game, exactly as clicking Play in its own library would.
    /// </summary>
    /// <returns>
    /// Whether the request was handed over, which is not whether the game started: Steam takes it
    /// from here, and may still be starting up, downloading an update, or showing a launch picker.
    /// </returns>
    /// <remarks>
    /// Handed to Steam rather than run here, so the game gets the launch options, the Proton build
    /// and the overlay Steam would give it. Steam is started first if it is not already running.
    /// </remarks>
    bool LaunchGame(uint appId);
}
