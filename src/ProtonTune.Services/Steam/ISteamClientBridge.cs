namespace ProtonTune.Services.Steam;

/// <summary>
/// Talks to the running Steam client through the debugging interface live editing switches on.
/// </summary>
/// <remarks>
/// Steam's own interface is a web page holding a privileged object through which the client can be
/// asked to change a game's configuration. Asked that way it applies the change at once and writes
/// it out itself, rather than the caller closing Steam and editing the files it owns.
/// </remarks>
public interface ISteamClientBridge
{
    /// <summary>
    /// Opens a connection to the running Steam client.
    /// </summary>
    /// <returns>
    /// A session to work through, or <see langword="null"/> when Steam cannot be reached — not
    /// running, live editing off, still starting, or between interfaces. None is an error: they
    /// all mean the caller should do it the long way.
    /// </returns>
    Task<ISteamClientSession?> ConnectAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A connection to the running Steam client, held open for as long as there is work to do on it.
/// </summary>
/// <remarks>
/// Held rather than reconnected per call, since a profile applied across a library is one action
/// and should not be a connection per game. Closed afterwards, since a socket held across a Steam
/// restart silently stops working.
/// </remarks>
public interface ISteamClientSession : IAsyncDisposable
{
    /// <summary>
    /// Reads what Steam currently holds for a game.
    /// </summary>
    /// <returns><see langword="null"/> when Steam did not answer for that game.</returns>
    Task<SteamAppDetails?> GetAppDetailsAsync(uint appId, CancellationToken cancellationToken = default);

    /// <summary>Sets a game's launch options. An empty value clears them.</summary>
    /// <returns>Whether Steam accepted the change.</returns>
    Task<bool> SetLaunchOptionsAsync(
        uint appId,
        string launchOptions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Points a game at a Proton build, named as Steam knows it. An empty name clears the choice
    /// and lets Steam decide.
    /// </summary>
    /// <returns>Whether Steam accepted the change.</returns>
    Task<bool> SetCompatToolAsync(uint appId, string toolName, CancellationToken cancellationToken = default);
}
