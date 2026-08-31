using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="ISteamClientSession" />
/// <remarks>
/// Works by evaluating JavaScript inside Steam's own interface, which is the only way in: the
/// object that can change a game's configuration exists there and nowhere else. Every expression
/// is written to answer with a small tagged object rather than a bare value, so "Steam does not
/// offer this" and "Steam did it" cannot be confused with each other or with a page that returned
/// nothing.
/// </remarks>
internal sealed class SteamClientSession(
    ClientWebSocket socket,
    SemaphoreSlim turns,
    ILogger logger) : ISteamClientSession
{
    /// <summary>
    /// How long to wait for Steam to answer one expression.
    /// </summary>
    /// <remarks>
    /// Long, because reading a game's details waits on Steam to call back with them, and a client
    /// busy starting up or shutting a game down can take its time about it.
    /// </remarks>
    private static readonly TimeSpan EvaluateTimeout = TimeSpan.FromSeconds(20);

    /// <summary>How long a read of one game waits inside Steam before giving up.</summary>
    private const int DetailsTimeoutMilliseconds = 5000;

    private int _lastId;

    /// <inheritdoc />
    public async Task<SteamAppDetails?> GetAppDetailsAsync(
        uint appId,
        CancellationToken cancellationToken = default)
    {
        // Steam does not return a game's details; it calls back with them, and keeps calling back
        // as they change. So the callback is turned into something that can be awaited once, and
        // the registration is given up straight afterwards — left in place, every game ever opened
        // would go on being reported for the life of the Steam session.
        var expression = $$"""
            (async () => {
              const apps = window.SteamClient?.Apps;
              if (!apps?.RegisterForAppDetails) return { tag: "no-api" };
              let registration = null;
              try {
                const details = await new Promise(resolve => {
                  let settled = false;
                  const finish = value => { if (!settled) { settled = true; resolve(value); } };
                  registration = apps.RegisterForAppDetails({{appId}}, finish);
                  setTimeout(() => finish(null), {{DetailsTimeoutMilliseconds}});
                });
                if (!details) return { tag: "no-details" };
                return {
                  tag: "ok",
                  launchOptions: details.strLaunchOptions ?? "",
                  compatToolName: details.strCompatToolName ?? ""
                };
              } finally {
                if (registration && typeof registration.unregister === "function") {
                  registration.unregister();
                }
              }
            })()
            """;

        if (await EvaluateAsync(expression, cancellationToken).ConfigureAwait(false) is not { } answer)
        {
            return null;
        }

        switch (Tag(answer))
        {
            case "ok":
                return new SteamAppDetails(
                    Text(answer, "launchOptions"),
                    Text(answer, "compatToolName"));

            case "no-details":
                logger.LogWarning("Steam did not report any details for {AppId}.", appId);

                return null;

            default:
                logger.LogWarning("Steam does not offer app details on this version.");

                return null;
        }
    }

    /// <inheritdoc />
    public Task<bool> SetLaunchOptionsAsync(
        uint appId,
        string launchOptions,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            "SetAppLaunchOptions",
            appId,
            launchOptions,
            cancellationToken);

    /// <inheritdoc />
    public Task<bool> SetCompatToolAsync(
        uint appId,
        string toolName,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            "SpecifyCompatTool",
            appId,
            toolName,
            cancellationToken);

    /// <summary>
    /// Calls one of Steam's own methods that takes a game and a string, which both of the changes
    /// made here happen to be.
    /// </summary>
    /// <remarks>
    /// The argument is written into the expression as JSON rather than quoted by hand. Launch
    /// options are full of quotes, backslashes and percent signs, and a string built by
    /// concatenation would eventually produce an expression that is no longer the one intended.
    /// </remarks>
    private async Task<bool> ApplyAsync(
        string method,
        uint appId,
        string argument,
        CancellationToken cancellationToken)
    {
        var expression = $$"""
            (async () => {
              const apps = window.SteamClient?.Apps;
              if (!apps?.{{method}}) return { tag: "no-api" };
              await apps.{{method}}({{appId}}, {{JsonSerializer.Serialize(argument)}});
              return { tag: "ok" };
            })()
            """;

        if (await EvaluateAsync(expression, cancellationToken).ConfigureAwait(false) is not { } answer)
        {
            return false;
        }

        if (Tag(answer) == "ok")
        {
            return true;
        }

        logger.LogWarning("Steam does not offer {Method} on this version.", method);

        return false;
    }

    /// <summary>
    /// Evaluates one expression inside Steam's interface and hands back what it answered with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One at a time, across every session. Steam's interface crashes outright — taking the whole
    /// client with it — when two evaluations against the same page are in flight together, which
    /// is exactly what applying a profile to a library would otherwise do.
    /// </para>
    /// <para>
    /// The reply is matched by the number sent with the request. Steam also sends messages of its
    /// own accord, which carry no such number and are passed over.
    /// </para>
    /// </remarks>
    private async Task<JsonElement?> EvaluateAsync(string expression, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _lastId);

        var request = JsonSerializer.Serialize(new
        {
            id,
            method = "Runtime.evaluate",
            @params = new
            {
                expression,
                awaitPromise = true,
                returnByValue = true
            }
        });

        await turns.WaitAsync(cancellationToken).ConfigureAwait(false);

        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        attempt.CancelAfter(EvaluateTimeout);

        try
        {
            await socket
                .SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, attempt.Token)
                .ConfigureAwait(false);

            while (true)
            {
                using var reply = JsonDocument.Parse(await ReceiveAsync(attempt.Token).ConfigureAwait(false));

                if (!reply.RootElement.TryGetProperty("id", out var replyId) || replyId.GetInt32() != id)
                {
                    continue;
                }

                if (reply.RootElement.TryGetProperty("error", out var error))
                {
                    logger.LogWarning("Steam refused the request: {Error}.", error.ToString());

                    return null;
                }

                if (!reply.RootElement.TryGetProperty("result", out var outcome))
                {
                    return null;
                }

                if (outcome.TryGetProperty("exceptionDetails", out var thrown))
                {
                    logger.LogWarning("Steam's interface threw: {Exception}.", thrown.ToString());

                    return null;
                }

                if (!outcome.TryGetProperty("result", out var value) ||
                    !value.TryGetProperty("value", out var answer))
                {
                    return null;
                }

                return answer.Clone();
            }
        }
        catch (Exception e) when (e is WebSocketException or JsonException or ObjectDisposedException)
        {
            logger.LogWarning(e, "Lost the connection to Steam part way through.");

            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Steam did not answer within {Timeout}.", EvaluateTimeout);

            return null;
        }
        finally
        {
            turns.Release();
        }
    }

    /// <summary>
    /// Reads one whole message. Steam's replies routinely run past a single frame, and a fragment
    /// parsed on its own is not valid JSON.
    /// </summary>
    private async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();

        while (true)
        {
            var part = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (part.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException("Steam closed the connection.");
            }

            message.Write(buffer, 0, part.Count);

            if (part.EndOfMessage)
            {
                return message.ToArray();
            }
        }
    }

    private static string Tag(JsonElement answer) =>
        answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("tag", out var tag)
            ? tag.GetString() ?? string.Empty
            : string.Empty;

    private static string Text(JsonElement answer, string property) =>
        answer.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(2));

                await socket
                    .CloseAsync(WebSocketCloseStatus.NormalClosure, null, closing.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // Closing politely is a courtesy to Steam, not something worth reporting: the work is
            // already done and the socket is about to go either way.
        }
        finally
        {
            socket.Dispose();
        }
    }
}
