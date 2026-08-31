namespace ProtonTune.Services.Steam;

/// <summary>
/// Reports whether Steam's debugging interface is currently accepting connections.
/// </summary>
/// <remarks>
/// Steam decides whether to open this port as it starts, so the presence of the file that asks
/// for it says what Steam will do next time rather than what it is doing now. The two disagree
/// for as long as it takes to restart Steam, and telling someone live editing is on while the
/// client it would talk to is not listening is the one thing this has to avoid.
/// </remarks>
public interface ISteamDebugPort
{
    /// <summary>
    /// Whether something is listening on the debugging port, without speaking to it.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> rather than throwing when the port is closed, unreachable, or slow
    /// to answer — every one of those means the same thing to a caller.
    /// </returns>
    Task<bool> IsListeningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for the port to start answering, for callers that have just started Steam.
    /// </summary>
    /// <returns>
    /// Whether it answered within <paramref name="timeout" />. A false is not an error — Steam may
    /// simply still be coming up — so callers report what they find rather than failing.
    /// </returns>
    /// <remarks>
    /// Here rather than in the caller so that how often to look, and how long to keep looking, are
    /// decisions belonging to the thing that knows the port. It also keeps the waiting out of
    /// services that are otherwise instant, which is what lets them be tested without sleeping.
    /// </remarks>
    Task<bool> WaitUntilListeningAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
