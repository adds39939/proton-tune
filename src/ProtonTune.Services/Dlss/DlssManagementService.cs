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
    /// <remarks>
    /// The install is searched recursively: an Unreal game keeps these libraries several
    /// directories down under <c>Engine/Plugins</c>, and may carry more than one copy.
    /// </remarks>
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
        var links = new List<DlssLink>();

        foreach (var library in status.Libraries)
        {
            if (!store.TryGetValue(library.FileName, out var source))
            {
                continue;
            }

            var backupPath = BackupPathFor(entry.AppId, library);

            BackUp(backupPath, library);
            Link(source, library.Path);

            links.Add(new DlssLink(source, library.Path, backupPath));
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

    /// <summary>Where a game's own copy of a library is kept while a link stands in for it.</summary>
    private string BackupPathFor(uint appId, DlssLibrary library) =>
        Path.Combine(storage.BackupsFor(appId), library.RelativePath);

    /// <summary>
    /// Moves a game's own library aside so the swap can be undone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only a real file is ever moved. A second apply sees a link rather than a file, and moving
    /// that aside would store a link where the original should be and lose the original for good.
    /// </para>
    /// <para>
    /// An earlier backup is replaced rather than kept. Steam puts the game's own file back
    /// whenever it verifies or updates it, so a real file here after an update is a newer original
    /// than whatever was stored the first time — keeping the older one would mean reverting the
    /// game to a library it no longer ships.
    /// </para>
    /// </remarks>
    private void BackUp(string backupPath, DlssLibrary library)
    {
        if (library.State != DlssLinkState.Original)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        File.Move(library.Path, backupPath, overwrite: true);

        logger.LogInformation("Kept {RelativePath} aside at {BackupPath}.", library.RelativePath, backupPath);
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
    /// <summary>One library the script maintains: what it points at, and where its own copy is.</summary>
    private sealed record DlssLink(string Source, string Destination, string Backup);

    /// <summary>
    /// Writes the script that re-applies a game's links every time it launches.
    /// </summary>
    /// <remarks>
    /// Before re-linking, a real file at the destination is copied to the backup when the backup
    /// is missing or a different size. Steam puts the game's own library back when it verifies or
    /// updates, and that copy is then the only original there is — losing it would make the swap
    /// impossible to undo. Size is enough to spot a file the game has updated: these run to tens
    /// of megabytes and a new release is never byte-identical in length.
    /// </remarks>
    private async Task<string> WriteLaunchScriptAsync(
        SteamLibraryEntry entry,
        IReadOnlyList<DlssLink> links,
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

        foreach (var (source, destination, backup) in links)
        {
            script
                .AppendLine($"src={Quote(source)}")
                .AppendLine($"dst={Quote(destination)}")
                .AppendLine($"bak={Quote(backup)}")
                .AppendLine("if [ -f \"$src\" ] && [ \"$(readlink -f \"$dst\" 2>/dev/null)\" != \"$src\" ]; then")
                .AppendLine("    if [ -f \"$dst\" ] && [ ! -L \"$dst\" ]; then")
                .AppendLine("        if [ ! -f \"$bak\" ] || " +
                            "[ \"$(stat -c%s \"$dst\")\" != \"$(stat -c%s \"$bak\")\" ]; then")
                .AppendLine("            mkdir -p \"$(dirname \"$bak\")\"")
                .AppendLine("            cp -f \"$dst\" \"$bak\"")
                .AppendLine("            echo \"protontune: kept the game's own $(basename \"$dst\")\" >&2")
                .AppendLine("        fi")
                .AppendLine("    fi")
                .AppendLine("    ln -sfn \"$src\" \"$dst\"")
                .AppendLine("    echo \"protontune: relinked $(basename \"$dst\")\" >&2")
                .AppendLine("fi")
                .AppendLine();
        }

        script.AppendLine("exec \"$@\"");

        var path = storage.ScriptFor(entry.AppId);

        await File.WriteAllTextAsync(path, script.ToString(), cancellationToken).ConfigureAwait(false);

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
