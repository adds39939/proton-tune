namespace ProtonTune.Services.Steam;

/// <summary>
/// Talks to the running Steam client through the debugging interface live editing switches on.
/// </summary>
/// <remarks>
/// Steam's own interface is a web page, and it is handed a privileged object through which the
/// client can be asked to change a game's configuration. Asked that way, Steam applies the change
/// at once and writes it out itself — which is the whole point, since the alternative is to close
/// Steam, edit the files it owns, and start it again.
/// </remarks>
public interface ISteamClientBridge
{
    /// <summary>
    /// Opens a connection to the running Steam client.
    /// </summary>
    /// <returns>
    /// A session to work through, or <see langword="null"/> when Steam cannot be reached — it is
    /// not running, live editing has not been switched on, it is still starting, or it is between
    /// interfaces. None of those is an error: they mean the caller should do it the long way, so
    /// they are reported as an absence rather than thrown.
    /// </returns>
    Task<ISteamClientSession?> ConnectAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A connection to the running Steam client, held open for as long as there is work to do on it.
/// </summary>
/// <remarks>
/// Worth holding rather than reconnecting per call, because a profile applied across a library is
/// one action to the person doing it and should not be a connection per game. Worth closing
/// afterwards rather than keeping, because a socket held across a Steam restart is a socket that
/// silently stops working.
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
