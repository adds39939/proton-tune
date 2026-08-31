using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="ISteamClientBridge" />
public sealed class SteamClientBridge(ILogger<SteamClientBridge> logger) : ISteamClientBridge
{
    /// <summary>How long to spend finding and connecting to Steam's interface.</summary>
    /// <remarks>
    /// Short on purpose. Every failure here ends in doing the work the long way instead, so a
    /// caller waiting on this is a caller not yet getting on with it.
    /// </remarks>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private static readonly HttpClient Http = new() { Timeout = ConnectTimeout };

    /// <summary>
    /// Held across every session, so that no two expressions are ever in flight against Steam's
    /// interface at once. Two at once crashes the interface and takes the client down with it,
    /// and the sessions that would collide belong to separate callers, so one lock per session
    /// would not be a lock at all.
    /// </summary>
    private readonly SemaphoreSlim _turns = new(1, 1);

    /// <inheritdoc />
    public async Task<ISteamClientSession?> ConnectAsync(CancellationToken cancellationToken = default)
    {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        attempt.CancelAfter(ConnectTimeout);

        ClientWebSocket? socket = null;

        try
        {
            var listing = await Http
                .GetStringAsync($"http://127.0.0.1:{SteamDebugPort.Port}/json", attempt.Token)
                .ConfigureAwait(false);

            if (SteamDevToolsTarget.FindSharedContext(listing) is not { } address)
            {
                logger.LogInformation("Steam is listening but is not showing its own interface yet.");

                return null;
            }

            socket = new ClientWebSocket();

            await socket.ConnectAsync(new Uri(address), attempt.Token).ConfigureAwait(false);

            return new SteamClientSession(socket, _turns, logger);
        }
        catch (Exception e) when (e is HttpRequestException or WebSocketException or UriFormatException)
        {
            // Steam not running, live editing not switched on, or an interface that is not the one
            // expected. All of them mean the same thing to the caller, so none of them is thrown.
            logger.LogDebug(e, "Steam could not be reached for live editing.");

            socket?.Dispose();

            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Steam did not answer within {Timeout}.", ConnectTimeout);

            socket?.Dispose();

            return null;
        }
        catch
        {
            socket?.Dispose();

            throw;
        }
    }
}
