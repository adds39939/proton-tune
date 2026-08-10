using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Dlss;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Dlss;
using ProtonTune.Services.Profiles;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Per-game configuration: reads what Steam has stored, hands it to the shared editor, and writes
/// it back.
/// </summary>
public partial class GameConfigDialog : ComponentBase
{
    [Inject]
    private ISteamLaunchOptionsService LaunchOptionsService { get; set; } = null!;

    [Inject]
    private IGlobalProfileService Profile { get; set; } = null!;

    [Inject]
    private IDlssManagementService Dlss { get; set; } = null!;

    [Inject]
    private IDlssRuntimeProvider DlssRuntimes { get; set; } = null!;

    /// <summary>The entry being configured.</summary>
    [Parameter]
    [EditorRequired]
    public required SteamLibraryEntry Entry { get; set; }

    /// <summary>Raised when the dialog asks to be dismissed.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>The configuration as it currently stands in the dialog.</summary>
    private LaunchOptions Editing { get; set; } = new();

    /// <summary>What Steam has stored, to compare against.</summary>
    private string Saved { get; set; } = string.Empty;

    private LaunchOptionsEditor? Editor { get; set; }

    private bool IsLoading { get; set; } = true;

    private string? LoadError { get; set; }

    private bool IsSaving { get; set; }

    private string? SaveMessage { get; set; }

    private bool SaveFailed { get; set; }

    private bool WillRestartSteam { get; set; }

    private bool GameIsRunning { get; set; }

    /// <summary>Whether this game is following the global profile.</summary>
    private bool UsesGlobal { get; set; }

    /// <summary>What was stored, so an unchanged link is not rewritten on save.</summary>
    private bool SavedUsesGlobal { get; set; }

    private bool HasChanges =>
        !string.Equals(Editing.Format(), Saved, StringComparison.Ordinal) || UsesGlobal != SavedUsesGlobal;

    /// <summary>Whether the reset button is waiting for a second click.</summary>
    private bool ResetPending { get; set; }

    /// <summary>Whether there is anything to reset at all.</summary>
    private bool HasAnythingToReset =>
        Saved.Length > 0 || SavedUsesGlobal || Dlss.Inspect(Entry).IsManaged;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        IsLoading = true;
        LoadError = null;
        SaveMessage = null;

        try
        {
            Editing = await LaunchOptionsService.GetAsync(Entry.AppId);
            Saved = Editing.Format();
            SavedUsesGlobal = await Profile.IsLinkedAsync(Entry.AppId);
            UsesGlobal = SavedUsesGlobal;

            RefreshSteamState();
        }
        catch (Exception e)
        {
            Editing = new LaunchOptions();
            LoadError = $"Could not read launch options: {e.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // Opening on a section that has something set needs the editor, which does not exist
        // until the first render has happened.
        if (firstRender)
        {
            Editor?.SelectFirstConfiguredCategory();
            StateHasChanged();
        }
    }

    /// <summary>
    /// Takes a change from the editor. Editing anything by hand stops the game following the
    /// global profile, but keeps whatever the profile had already put there — the settings are
    /// the game's own from that point on.
    /// </summary>
    private void OnOptionsChanged(LaunchOptions options)
    {
        Editing = options;
        UsesGlobal = false;
        SaveMessage = null;
        ResetPending = false;
    }

    /// <summary>
    /// Replaces the game's settings with the global profile's, or stops following it.
    /// </summary>
    /// <remarks>
    /// Turning it off leaves the settings exactly as they are. The profile is a starting point
    /// rather than an owner: unlinking should not silently undo a configuration the user can see
    /// in front of them.
    /// </remarks>
    private async Task OnUseGlobalChanged(bool useGlobal)
    {
        UsesGlobal = useGlobal;
        SaveMessage = null;

        if (!useGlobal)
        {
            return;
        }

        var global = await Profile.GetAsync();

        // The DLSS launch script is generated per game and names its own app id, so it cannot
        // come from a shared profile. Carry it across rather than dropping it.
        var scriptPath = Dlss.ScriptPathFor(Entry.AppId);
        var hadScript = Editing.HasWrapperCommand(scriptPath);

        Editing = hadScript ? global.WithWrapperCommand(scriptPath, true) : global;
    }

    private void Revert()
    {
        Editing = LaunchOptions.Parse(Saved);
        UsesGlobal = SavedUsesGlobal;
        SaveMessage = null;
        ResetPending = false;
    }

    /// <summary>
    /// Puts the game back to how it was before ProtonTune touched it: no launch options, not
    /// following the profile, and its own DLSS libraries restored.
    /// </summary>
    /// <remarks>
    /// Asks first. This clears settings the user cannot see from here — the DLSS libraries in
    /// particular are files inside the install — so a single mis-click should not do it.
    /// </remarks>
    private async Task ResetAsync()
    {
        if (!ResetPending)
        {
            ResetPending = true;

            return;
        }

        ResetPending = false;
        IsSaving = true;
        SaveMessage = null;

        try
        {
            // Libraries first: the launch script that re-applies them is about to be removed, and
            // leaving links behind with nothing maintaining them is the one state worse than
            // either extreme.
            if (Dlss.Inspect(Entry).IsManaged)
            {
                await Dlss.RevertAsync(Entry);
            }

            var result = await LaunchOptionsService.SaveAsync(Entry.AppId, string.Empty);

            SaveFailed = !result.IsSuccess;

            if (result.IsSuccess)
            {
                await Profile.SetLinkedAsync(Entry.AppId, false);

                Editing = new LaunchOptions();
                Saved = string.Empty;
                UsesGlobal = false;
                SavedUsesGlobal = false;

                SaveMessage = "Reset. The game has no launch options and its own DLSS libraries." +
                              (result.SteamWasRestarted ? " Steam was closed and started again." : string.Empty);
            }
            else
            {
                SaveMessage = result.Message ?? "The game could not be reset.";
            }
        }
        catch (Exception e)
        {
            SaveFailed = true;
            SaveMessage = $"The game could not be reset: {e.Message}";
        }
        finally
        {
            IsSaving = false;
            RefreshSteamState();
        }
    }

    /// <summary>Re-checks Steam's state, which can change while the dialog is open.</summary>
    private void RefreshSteamState()
    {
        GameIsRunning = LaunchOptionsService.IsGameRunning();
        WillRestartSteam = LaunchOptionsService.RequiresSteamRestart();
    }

    private async Task SaveAsync()
    {
        IsSaving = true;
        SaveMessage = null;

        try
        {
            var result = await LaunchOptionsService.SaveAsync(Entry.AppId, Editing.Format());

            SaveFailed = !result.IsSuccess;

            if (result.IsSuccess)
            {
                Saved = Editing.Format();

                await Profile.SetLinkedAsync(Entry.AppId, UsesGlobal);
                SavedUsesGlobal = UsesGlobal;

                SaveMessage = result.SteamWasRestarted
                    ? "Saved. Steam was closed and started again so the change would stick."
                    : "Saved.";
            }
            else
            {
                SaveMessage = result.Message ?? "The launch options could not be saved.";
            }
        }
        catch (Exception e)
        {
            SaveFailed = true;
            SaveMessage = $"The launch options could not be saved: {e.Message}";
        }
        finally
        {
            IsSaving = false;
            RefreshSteamState();
        }
    }

    private Task Close() => OnClose.InvokeAsync();

    /// <summary>
    /// Dismisses on Escape, unless a save is in flight — Steam is mid-restart at that point and
    /// closing the dialog would hide what is happening.
    /// </summary>
    private Task OnKeyDown(KeyboardEventArgs args) =>
        args.Key == "Escape" && !IsSaving ? Close() : Task.CompletedTask;
}
