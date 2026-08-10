using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Launch;
using ProtonTune.Services.Profiles;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Edits the global profile — the same settings a game gets, kept once and applied where wanted.
/// </summary>
/// <remarks>
/// Saving here writes only to ProtonTune's own storage, so Steam does not have to be restarted.
/// Games already following the profile are not rewritten: doing so would mean editing several
/// games' launch options behind a save the user made on a different screen.
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

    /// <summary>Whether the reset button is waiting for a second click.</summary>
    private bool ResetPending { get; set; }

    /// <summary>Whether saving would close and reopen Steam, which only cascading requires.</summary>
    private bool WillRestartSteam => LinkedCount > 0 && Steam.RequiresSteamRestart();

    private bool HasChanges => !string.Equals(Editing.Format(), Saved, StringComparison.Ordinal);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Editing = await Profile.GetAsync();
            Saved = Editing.Format();

            await CountLinkedAsync();
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
    /// Asks first, then clears. Wiping a profile and unlinking every game that follows it is not
    /// something to do on a mis-click.
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
        }
    }
}
