using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Settings;
using ProtonTune.Services.Settings;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Lists the copies ProtonTune has taken of Steam's configuration files, and puts one back.
/// </summary>
/// <remarks>
/// These files are changed by splicing single values into text Steam owns. That is deliberately
/// narrow, but it is still an edit to a file holding an entire Steam configuration — so every
/// write keeps a copy first, and this is where one is reached for when a change turns out wrong.
/// </remarks>
public partial class SteamBackupsPanel : ComponentBase
{
    [Inject]
    private ISteamConfigBackupService Backups { get; set; } = null!;

    [Inject]
    private IAppSettingsService Settings { get; set; } = null!;

    private IReadOnlyList<SteamConfigBackup> Available { get; set; } = [];

    private AppSettings Current { get; set; } = new();

    private bool IsLoading { get; set; } = true;

    private bool IsBusy { get; set; }

    private string? Message { get; set; }

    private bool Failed { get; set; }

    /// <summary>The backup a second click would restore, so one click cannot replace a file.</summary>
    private string? PendingRestore { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Current = await Settings.GetAsync();

            await RefreshAsync();
        }
        catch (Exception e)
        {
            Failed = true;
            Message = $"Could not read the backups: {e.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshAsync() => Available = await Backups.ListAsync();

    /// <summary>
    /// Changes how many are kept, and applies it at once. Choosing a smaller number and finding
    /// the old copies still there would leave it unclear whether anything had happened.
    /// </summary>
    private async Task OnRetentionChanged(ChangeEventArgs args)
    {
        if (!int.TryParse(args.Value?.ToString(), out var keep))
        {
            return;
        }

        IsBusy = true;
        Message = null;

        try
        {
            Current = new AppSettings { BackupsToKeep = keep }.Sanitised();

            await Settings.SaveAsync(Current);

            var removed = await Backups.PruneAsync(Current.BackupsToKeep);

            await RefreshAsync();

            Failed = false;
            Message = removed == 0
                ? $"Keeping the newest {Current.BackupsToKeep} of each file."
                : $"Keeping the newest {Current.BackupsToKeep} of each file. Removed {removed} older " +
                  $"{(removed == 1 ? "copy" : "copies")}.";
        }
        catch (Exception e)
        {
            Failed = true;
            Message = $"Could not change how many backups are kept: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Asks first. Restoring replaces a file holding an entire Steam configuration.</summary>
    private async Task RestoreAsync(SteamConfigBackup backup)
    {
        if (PendingRestore != backup.Path)
        {
            PendingRestore = backup.Path;
            Message = null;

            return;
        }

        PendingRestore = null;
        IsBusy = true;
        Message = null;

        try
        {
            var result = await Backups.RestoreAsync(backup);

            Failed = !result.IsSuccess;

            Message = result.IsSuccess
                ? Restored(backup, result)
                : result.Message ?? "The backup could not be restored.";

            await RefreshAsync();
        }
        catch (Exception e)
        {
            Failed = true;
            Message = $"The backup could not be restored: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Says what the restore did, including what it had to correct afterwards. A restored file can
    /// hold launch options from before a game followed the global profile, and leaving ProtonTune
    /// claiming otherwise would mean the next profile save quietly rewrote that game.
    /// </summary>
    private static string Restored(SteamConfigBackup backup, SteamConfigRestoreResult result)
    {
        var message = $"Restored {backup.TargetName} from {backup.CreatedAt:d MMM HH:mm}.";

        if (result.SteamWasRestarted)
        {
            message += " Steam was closed and started again.";
        }

        if (result.UnlinkedFromProfile > 0)
        {
            message += $" {result.UnlinkedFromProfile} " +
                       $"{(result.UnlinkedFromProfile == 1 ? "game no longer matches" : "games no longer match")} " +
                       "the global profile, so they no longer follow it.";
        }

        return message;
    }

    private static string Size(long bytes) => $"{bytes / 1024.0:0} KiB";
}
