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
    public bool IsGameRunning()
    {
        try
        {
            // Reading /proc directly rather than asking each Process for its command line, which
            // the framework does not expose on Linux.
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
                    // The process exited between listing and reading, or belongs to someone else.
                    continue;
                }

                // Arguments are NUL separated in /proc.
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

        // Steam writes its configuration on the way out, so the file is only safe to touch once
        // the process has actually gone — not merely once the request has been accepted.
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
    /// Detached where possible, falling back to a plain launch on a system without
    /// <see cref="DetachCommand" />. Going straight to the fallback would leave Steam tied to
    /// ProtonTune, which is the bug this exists to avoid, so it is only reached when the first
    /// attempt cannot start at all.
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
    /// Steam is started in a session of its own. A process started the ordinary way inherits
    /// ProtonTune's process group, so anything that signals that group — a terminal closing, a
    /// desktop session ending ProtonTune, a stop sent to the whole group — reaches Steam as well
    /// and takes it down with the app that restarted it. Measured directly: with the plain launch
    /// the child dies on a group signal, and in its own session it survives.
    /// </para>
    /// <para>
    /// Nothing is redirected either. The output used to be captured into pipes that were never
    /// read, so Steam would block once the buffer filled — it is talkative on startup — and then
    /// take a broken pipe when ProtonTune exited. Steam does its own logging, so letting the
    /// streams alone is both simpler and safer than draining pipes nobody wants.
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
            // --fork guarantees a new session whether or not this process happens to lead its
            // group; setsid alone is a no-op for a group leader.
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
