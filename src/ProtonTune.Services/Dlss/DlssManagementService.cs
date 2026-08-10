using System.Text;
using Microsoft.Extensions.Logging;
using ProtonTune.Core.Dlss;
using ProtonTune.Core.Steam;

namespace ProtonTune.Services.Dlss;

/// <inheritdoc cref="IDlssManagementService" />
/// <remarks>
/// Games are pointed at ProtonTune's copy with a symlink rather than having their files
/// overwritten, so the original is never destroyed and the swap is visible to anyone looking at
/// the directory.
/// </remarks>
public sealed class DlssManagementService(
    IDlssRuntimeProvider runtimeProvider,
    ProtonTuneStorage storage,
    ILogger<DlssManagementService> logger) : IDlssManagementService
{
    /// <inheritdoc />
    public string ScriptPathFor(uint appId) => storage.ScriptFor(appId);

    /// <inheritdoc />
    public DlssGameStatus Inspect(SteamLibraryEntry entry)
    {
        if (!Directory.Exists(entry.InstallDirectory))
        {
            return new DlssGameStatus();
        }

        var replaceable = runtimeProvider.GetAll()
            .SelectMany(runtime => runtime.FileNames)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        try
        {
            var libraries = replaceable
                // Searched recursively: an Unreal game keeps these several directories down,
                // under Engine/Plugins, and may carry more than one copy.
                .SelectMany(name => Directory.EnumerateFiles(entry.InstallDirectory, name, SearchOption.AllDirectories))
                .Distinct(StringComparer.Ordinal)
                .Select(path => Describe(path, entry.InstallDirectory))
                .OrderBy(library => library.RelativePath, StringComparer.Ordinal)
                .ToList();

            return new DlssGameStatus { Libraries = libraries };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not search {InstallDirectory} for DLSS libraries.", entry.InstallDirectory);

            return new DlssGameStatus();
        }
    }

    /// <summary>Works out what a library is: the game's own file, our link, or someone else's.</summary>
    private DlssLibrary Describe(string path, string installDirectory)
    {
        var relativePath = Path.GetRelativePath(installDirectory, path);
        var target = new FileInfo(path).LinkTarget;

        if (target is null)
        {
            return new DlssLibrary(path, relativePath, DlssLinkState.Original, null);
        }

        var isManaged = Path.GetFullPath(target).StartsWith(storage.LibraryStore, StringComparison.Ordinal);

        return new DlssLibrary(
            path,
            relativePath,
            isManaged ? DlssLinkState.Managed : DlssLinkState.Foreign,
            target);
    }

    /// <inheritdoc />
    public async Task<string> ApplyAsync(
        SteamLibraryEntry entry,
        DlssRuntime runtime,
        CancellationToken cancellationToken = default)
    {
        var store = await EnsureStoredAsync(runtime, cancellationToken).ConfigureAwait(false);
        var status = Inspect(entry);
        var links = new List<(string Source, string Destination)>();

        foreach (var library in status.Libraries)
        {
            if (!store.TryGetValue(library.FileName, out var source))
            {
                continue;
            }

            BackUp(entry.AppId, library);
            Link(source, library.Path);

            links.Add((source, library.Path));
        }

        var scriptPath = await WriteLaunchScriptAsync(entry, links, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Linked {LinkCount} DLSS libraries for {AppId} to version {Version}.",
            links.Count,
            entry.AppId,
            runtime.Version);

        return scriptPath;
    }

    /// <inheritdoc />
    public async Task<DlssRevertResult> RevertAsync(
        SteamLibraryEntry entry,
        CancellationToken cancellationToken = default)
    {
        var backupRoot = storage.BackupsFor(entry.AppId);
        var restored = new List<string>();

        if (Directory.Exists(backupRoot))
        {
            foreach (var backup in Directory.EnumerateFiles(backupRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(backupRoot, backup);
                var destination = Path.Combine(entry.InstallDirectory, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Delete(destination);
                File.Move(backup, destination);

                restored.Add(relativePath);
            }

            Directory.Delete(backupRoot, recursive: true);
        }

        // Anything still linked has outlived its backup, so there is no original left to put back.
        // Leaving it would mean reporting the game restored while a file inside it still pointed at
        // ProtonTune — and nothing would ever clear it, because the next revert finds no backup
        // either. Turning the link into a real file at least leaves the game owning its own files.
        var replaced = new List<string>();

        foreach (var library in Inspect(entry).Libraries.Where(library => library.State == DlssLinkState.Managed))
        {
            if (library.LinkTarget is not { } target || !File.Exists(target))
            {
                continue;
            }

            File.Delete(library.Path);
            File.Copy(target, library.Path);

            replaced.Add(library.RelativePath);

            logger.LogWarning(
                "{RelativePath} had no backup for {AppId}, so a copy of the linked version was left in its place.",
                library.RelativePath,
                entry.AppId);
        }

        var scriptPath = ScriptPathFor(entry.AppId);

        if (File.Exists(scriptPath))
        {
            File.Delete(scriptPath);
        }

        logger.LogInformation(
            "Reverted DLSS for {AppId}: {RestoredCount} restored, {ReplacedCount} replaced with a copy.",
            entry.AppId,
            restored.Count,
            replaced.Count);

        await Task.CompletedTask.ConfigureAwait(false);

        return new DlssRevertResult(restored, replaced);
    }

    /// <summary>
    /// Copies a shipped version into ProtonTune's own store, which is what game files are linked
    /// to. Copied once and reused; the application directory is not a stable link target.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> EnsureStoredAsync(
        DlssRuntime runtime,
        CancellationToken cancellationToken)
    {
        var directory = storage.StoreFor(runtime.Version);

        Directory.CreateDirectory(directory);

        var stored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, source) in runtime.Files)
        {
            var destination = Path.Combine(directory, name);

            if (!File.Exists(destination) || new FileInfo(destination).Length != new FileInfo(source).Length)
            {
                await using var input = File.OpenRead(source);
                await using var output = File.Create(destination);

                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            stored[name] = destination;
        }

        return stored;
    }

    /// <summary>
    /// Moves a game's own library aside, once. A second apply must not overwrite the first
    /// backup with a link, which would lose the original for good.
    /// </summary>
    private void BackUp(uint appId, DlssLibrary library)
    {
        if (library.State != DlssLinkState.Original)
        {
            return;
        }

        var backupPath = Path.Combine(storage.BackupsFor(appId), library.RelativePath);

        if (File.Exists(backupPath))
        {
            logger.LogInformation("Keeping the existing backup of {RelativePath}.", library.RelativePath);

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        File.Move(library.Path, backupPath);
    }

    /// <summary>Replaces a path with a symlink, removing whatever was there.</summary>
    private static void Link(string source, string destination)
    {
        if (File.Exists(destination) || new FileInfo(destination).LinkTarget is not null)
        {
            File.Delete(destination);
        }

        File.CreateSymbolicLink(destination, source);
    }

    /// <summary>
    /// Writes the script the game is launched through.
    /// </summary>
    /// <remarks>
    /// The links cannot simply be made once. Steam restores a game's own files whenever it
    /// verifies or updates it, which silently undoes the swap — so the script re-applies them on
    /// every launch and then hands over to the game.
    /// </remarks>
    private async Task<string> WriteLaunchScriptAsync(
        SteamLibraryEntry entry,
        IReadOnlyList<(string Source, string Destination)> links,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(storage.Scripts);

        var script = new StringBuilder()
            .AppendLine("#!/usr/bin/env bash")
            .AppendLine($"# Generated by ProtonTune for {entry.Name} ({entry.AppId}).")
            .AppendLine("#")
            .AppendLine("# Steam restores a game's own DLSS libraries when it verifies or updates it, which")
            .AppendLine("# undoes the swap without saying so. Re-applying on every launch is what makes it")
            .AppendLine("# stick. Delete this file and ProtonTune's entry in the launch options to stop.")
            .AppendLine("set -u")
            .AppendLine();

        foreach (var (source, destination) in links)
        {
            script
                .AppendLine($"src={Quote(source)}")
                .AppendLine($"dst={Quote(destination)}")
                .AppendLine("if [ -f \"$src\" ] && [ \"$(readlink -f \"$dst\" 2>/dev/null)\" != \"$src\" ]; then")
                .AppendLine("    ln -sfn \"$src\" \"$dst\"")
                .AppendLine("    echo \"protontune: relinked $(basename \"$dst\")\" >&2")
                .AppendLine("fi")
                .AppendLine();
        }

        script.AppendLine("exec \"$@\"");

        var path = storage.ScriptFor(entry.AppId);

        await File.WriteAllTextAsync(path, script.ToString(), cancellationToken).ConfigureAwait(false);

        // Guarded rather than asserted: ProtonTune only runs on Linux, but the permission bits
        // have no meaning elsewhere and the analyzer is right to ask.
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        return path;
    }

    /// <summary>Quotes a path for the generated script.</summary>
    private static string Quote(string value) => $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
}
