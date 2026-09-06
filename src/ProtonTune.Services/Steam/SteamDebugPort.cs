using System.Net;
using System.Net.Sockets;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="ISteamDebugPort" />
public sealed class SteamDebugPort : ISteamDebugPort
{
    /// <summary>
    /// The port Steam's debugging interface listens on. Fixed by Steam rather than chosen here.
    /// </summary>
    public const int Port = 8080;

    /// <summary>
    /// How long to wait for the connection. Always to this machine, so anything beyond a moment
    /// means nothing is there.
    /// </summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    /// <summary>How often to look again while waiting for Steam to come up.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    /// <remarks>
    /// Opens a connection and closes it without sending anything: whether the port is open is the
    /// whole question, and asking for the target list would make Steam serve a request per visit
    /// to the settings screen.
    /// </remarks>
    public async Task<bool> IsListeningAsync(CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        attempt.CancelAfter(ConnectTimeout);

        try
        {
            await client.ConnectAsync(IPAddress.Loopback, Port, attempt.Token).ConfigureAwait(false);

            return true;
        }
        catch (Exception e) when (e is SocketException or ObjectDisposedException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> WaitUntilListeningAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (true)
        {
            if (await IsListeningAsync(cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}
