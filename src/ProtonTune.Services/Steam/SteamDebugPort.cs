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
    /// How long to wait for the connection. Only ever a connection to this machine, so anything
    /// beyond a moment means nothing is there rather than that the answer is slow.
    /// </summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    /// <summary>How often to look again while waiting for Steam to come up.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    /// <remarks>
    /// Opens a connection and closes it again without sending anything. Asking for the target
    /// list over HTTP would say more, but it would also be a request Steam has to serve on every
    /// visit to the settings screen, and whether the port is open is the whole question here.
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
            // The deadline above, not the caller giving up. A caller's own cancellation is theirs
            // to hear about, so only this one is answered with "nothing is listening".
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
