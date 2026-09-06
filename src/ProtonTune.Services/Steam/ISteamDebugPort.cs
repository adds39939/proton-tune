namespace ProtonTune.Services.Steam;

/// <summary>
/// Reports whether Steam's debugging interface is currently accepting connections.
/// </summary>
/// <remarks>
/// Steam opens the port as it starts, so the file asking for it describes the next run rather than
/// this one. The two disagree until Steam is restarted, and reporting live editing as on while
/// nothing is listening is what this exists to avoid.
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
    /// Here rather than in the caller, so the polling interval belongs to the thing that knows the
    /// port and the otherwise instant services stay testable without sleeping.
    /// </remarks>
    Task<bool> WaitUntilListeningAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
