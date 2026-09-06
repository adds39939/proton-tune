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
}
