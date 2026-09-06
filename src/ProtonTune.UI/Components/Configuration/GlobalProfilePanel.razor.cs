using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Launch;
using ProtonTune.Services.Profiles;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Edits the global profile — the same settings a game gets, kept once and applied where wanted.
/// </summary>
/// <remarks>
/// Saving writes only to ProtonTune's own storage, so Steam is not restarted. Games already
/// following the profile are not rewritten behind a save made on a different screen.
/// </remarks>
public partial class GlobalProfilePanel : ComponentBase
{
    [Inject]
    private IGlobalProfileService Profile { get; set; } = null!;

    [Inject]
    private ISteamLibraryService Library { get; set; } = null!;

    [Inject]
    private ISteamLaunchOptionsService Steam { get; set; } = null!;

    private LaunchOptions Editing { get; set; } = new();

    private string Saved { get; set; } = string.Empty;

    private bool IsLoading { get; set; } = true;

    private bool IsSaving { get; set; }

    private string? SaveMessage { get; set; }

    private bool SaveFailed { get; set; }

    /// <summary>How many installed games currently follow the profile.</summary>
    private int LinkedCount { get; set; }

    /// <summary>Whether the confirmation is open, waiting for the save to be agreed to.</summary>
    private bool IsConfirmingSave { get; set; }

    /// <summary>
    /// What saving does beyond storing the profile. Cascading is the part worth confirming, since
    /// it rewrites the launch options of games the user is not looking at.
    /// </summary>
    private IReadOnlyList<string> PendingSideEffects => LinkedCount == 0
        ? []
        : [$"Apply these settings to the {LinkedCount} {(LinkedCount == 1 ? "game" : "games")} following this profile."];

    /// <summary>Whether the reset button is waiting for a second click.</summary>
    private bool ResetPending { get; set; }

    /// <summary>How a save made now would reach Steam, refreshed alongside the linked count.</summary>
    private SteamSaveMethod SaveMethod { get; set; }

    /// <summary>
    /// Whether saving would close and reopen Steam, which only cascading requires — and only
    /// where the running client will not take the change directly.
    /// </summary>
    private bool WillRestartSteam => LinkedCount > 0 && SaveMethod == SteamSaveMethod.Restart;

    private bool HasChanges => !string.Equals(Editing.Format(), Saved, StringComparison.Ordinal);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Editing = await Profile.GetAsync();
            Saved = Editing.Format();

            await CountLinkedAsync();

            SaveMethod = await Steam.GetSaveMethodAsync();
        }
        catch (Exception e)
        {
            SaveFailed = true;
            SaveMessage = $"Could not read the global profile: {e.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Counts against the installed library rather than the stored list, so a game that has since
    /// been uninstalled is not reported as following anything.
    /// </summary>
    private async Task CountLinkedAsync()
    {
        var installed = await Library.GetInstalledAppsAsync();
        var linked = 0;

        foreach (var entry in installed)
        {
            if (await Profile.IsLinkedAsync(entry.AppId))
            {
                linked++;
            }
        }

        LinkedCount = linked;
    }

    private void OnOptionsChanged(LaunchOptions options)
    {
        Editing = options;
        SaveMessage = null;
        ResetPending = false;
    }

    private void Revert()
    {
        Editing = LaunchOptions.Parse(Saved);
        SaveMessage = null;
        ResetPending = false;
    }

    /// <summary>
    /// Asks first, since this wipes the profile and unlinks every game following it.
    /// </summary>
    private async Task ResetAsync()
    {
        if (!ResetPending)
        {
            ResetPending = true;

            return;
        }

        ResetPending = false;
        IsSaving = true;

        try
        {
            await Profile.ResetAsync();

            Editing = new LaunchOptions();
            Saved = string.Empty;
            LinkedCount = 0;
            SaveFailed = false;
            SaveMessage = "The profile is empty and no games follow it. Their own settings are untouched.";
        }
        catch (Exception e)
        {
            SaveFailed = true;
            SaveMessage = $"The global profile could not be reset: {e.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Opens the confirmation rather than saving, since a profile save can rewrite several games'
    /// launch options at once.
    /// </summary>
    private void AskToSave()
    {
        SaveMessage = null;
        ResetPending = false;
        IsConfirmingSave = true;
    }

    private void CancelSave() => IsConfirmingSave = false;

    private async Task SaveAsync()
    {
        IsSaving = true;
        SaveMessage = null;

        try
        {
            var result = await Profile.SaveAndApplyAsync(Editing);

            SaveFailed = !result.IsSuccess;

            if (result.IsSuccess)
            {
                Saved = Editing.Format();

                SaveMessage = LinkedCount == 0
                    ? "Saved."
                    : $"Saved and applied to {LinkedCount} {(LinkedCount == 1 ? "game" : "games")}." +
                      (result.SteamWasRestarted ? " Steam was closed and started again." : string.Empty);
            }
            else
            {
                SaveMessage = result.Message ?? "The global profile could not be saved.";
            }
        }
        catch (Exception e)
        {
            SaveFailed = true;
            SaveMessage = $"The global profile could not be saved: {e.Message}";
        }
        finally
        {
            IsSaving = false;
            IsConfirmingSave = false;
            SaveMethod = await Steam.GetSaveMethodAsync();
        }
    }
}
