namespace ProtonTune.Services.Steam;

/// <summary>
/// Observes and controls the running Steam client.
/// </summary>
public interface ISteamClient
{
    /// <summary>Whether the Steam client is currently running.</summary>
    bool IsRunning();

    /// <summary>
    /// Whether Steam is currently running a game. Shutting Steam down under a running game would
    /// end someone's session mid-play, so this is checked before anything else.
    /// </summary>
    bool IsGameRunning();

    /// <summary>
    /// Asks Steam to close and waits for it to actually exit.
    /// </summary>
    /// <returns><see langword="true"/> once Steam has exited, false if it outlasted the timeout.</returns>
    /// <remarks>
    /// Waiting for the process to go is the point rather than a courtesy: Steam writes its
    /// configuration on the way out, so those files are only safe to edit once it has gone, not
    /// merely once the shutdown request has been accepted.
    /// </remarks>
    Task<bool> ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>Starts the Steam client without waiting for it.</summary>
    bool Start();
}
