using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ProtonTune.Services.Steam;

/// <inheritdoc cref="ISteamClient" />
public sealed class SteamClient(ILogger<SteamClient> logger) : ISteamClient
{
    /// <summary>The process name of the Steam client itself.</summary>
    private const string ProcessName = "steam";

    /// <summary>
    /// Puts a launched process in a session of its own, so it outlives the app that started it.
    /// </summary>
    private const string DetachCommand = "setsid";

    /// <summary>
    /// Steam puts this in the command line of everything it launches a game through, so its
    /// presence anywhere in the process table means a game is running.
    /// </summary>
    private const string GameLaunchMarker = "SteamLaunch AppId=";

    /// <summary>How often to check whether Steam has finished exiting.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    /// <inheritdoc />
    public bool IsRunning()
    {
        try
        {
            return Process.GetProcessesByName(ProcessName).Length > 0;
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException)
        {
            logger.LogWarning(e, "Could not determine whether Steam is running.");

            return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Command lines are read from <c>/proc</c> directly, since .NET does not expose a process's
    /// arguments on Linux. Arguments there are NUL separated rather than spaced.
    /// </remarks>
    public bool IsGameRunning()
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories("/proc"))
            {
                var name = Path.GetFileName(directory);

                if (!int.TryParse(name, out _))
                {
                    continue;
                }

                string commandLine;

                try
                {
                    commandLine = File.ReadAllText(Path.Combine(directory, "cmdline"));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                if (commandLine.Replace('\0', ' ').Contains(GameLaunchMarker, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not scan for running games.");
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<bool> ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!IsRunning())
        {
            return true;
        }

        logger.LogInformation("Asking Steam to shut down.");

        if (!TryRun("-shutdown"))
        {
            return false;
        }

        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (!IsRunning())
            {
                logger.LogInformation("Steam has exited.");

                return true;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        logger.LogWarning("Steam was still running {Timeout} after being asked to shut down.", timeout);

        return false;
    }

    /// <inheritdoc />
    public bool Start()
    {
        logger.LogInformation("Starting Steam.");

        return TryRun();
    }

    /// <summary>
    /// Runs the <c>steam</c> launcher without waiting for it. The launcher forwards to the real
    /// client and returns immediately either way.
    /// </summary>
    /// <remarks>
    /// Detached where possible, falling back to a plain launch only when
    /// <see cref="DetachCommand" /> cannot start at all — the fallback leaves Steam tied to
    /// ProtonTune, which is the bug this exists to avoid.
    /// </remarks>
    private bool TryRun(params string[] arguments) =>
        TryStart(BuildStartInfo(detached: true, arguments)) ||
        TryStart(BuildStartInfo(detached: false, arguments));

    private bool TryStart(ProcessStartInfo startInfo)
    {
        try
        {
            using var process = Process.Start(startInfo);

            return process is not null;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not run {FileName}.", startInfo.FileName);

            return false;
        }
    }

    /// <summary>
    /// Describes how the Steam launcher is run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Steam gets a session of its own: a child in ProtonTune's process group dies with it on any
    /// group signal, which was measured directly.
    /// </para>
    /// <para>
    /// Nothing is redirected. Output captured into pipes nobody reads blocks Steam once the buffer
    /// fills, and Steam does its own logging anyway.
    /// </para>
    /// <para>
    /// <c>--fork</c> is what guarantees the new session: <c>setsid</c> without it is a no-op when
    /// the calling process already leads its group.
    /// </para>
    /// </remarks>
    internal static ProcessStartInfo BuildStartInfo(bool detached, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(detached ? DetachCommand : ProcessName)
        {
            UseShellExecute = false
        };

        if (detached)
        {
            startInfo.ArgumentList.Add("--fork");
            startInfo.ArgumentList.Add(ProcessName);
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
