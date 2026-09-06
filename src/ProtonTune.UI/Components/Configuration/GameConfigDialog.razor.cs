using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Proton;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Profiles;
using ProtonTune.Services.Proton;
using ProtonTune.Services.Steam;
using ProtonTune.UI.Components.Proton;

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
    private IProtonToolService ProtonTools { get; set; } = null!;

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

    /// <summary>Whether the opening section has been chosen for the game now being shown.</summary>
    private bool _hasChosenSection;

    private bool IsLoading { get; set; } = true;

    private string? LoadError { get; set; }

    private bool IsSaving { get; set; }

    private string? SaveMessage { get; set; }

    private bool SaveFailed { get; set; }

    /// <summary>How a save made now would reach Steam, which is what the footer warns about.</summary>
    private SteamSaveMethod SaveMethod { get; set; }

    /// <summary>Whether saving would close Steam and start it again.</summary>
    private bool WillRestartSteam => SaveMethod == SteamSaveMethod.Restart;

    /// <summary>
    /// Whether a running game stands in the way. Only when Steam would have to be closed for the
    /// save — with live editing the change goes into the running client and the game plays on.
    /// </summary>
    private bool GameIsRunning => SaveMethod == SteamSaveMethod.Blocked;

    /// <summary>Whether this game is following the global profile.</summary>
    private bool UsesGlobal { get; set; }

    /// <summary>What was stored, so an unchanged link is not rewritten on save.</summary>
    private bool SavedUsesGlobal { get; set; }

    /// <summary>
    /// The Proton build the game is pointed at, empty when it has none of its own. Held rather
    /// than applied on selection, so it is written in the same trip through Steam as the launch
    /// options: separate files, one shutdown.
    /// </summary>
    private string CompatTool { get; set; } = ProtonVersionEditor.InheritValue;

    private string SavedCompatTool { get; set; } = ProtonVersionEditor.InheritValue;

    /// <summary>The installed builds, kept so the pending choice can be resolved to one.</summary>
    private ProtonCatalogue ProtonBuilds { get; set; } = ProtonCatalogue.Empty;

    /// <summary>
    /// The build the game would run under if saved now — the pending choice, or the default when
    /// it has none of its own.
    /// </summary>
    /// <remarks>
    /// Follows the pending choice rather than the stored one, so switching build immediately
    /// re-judges every setting instead of waiting for a save.
    /// </remarks>
    private ProtonBuild? EffectiveBuild => CompatTool.Length > 0
        ? ProtonBuilds.FindBuild(CompatTool)
        : ProtonBuilds.Default.Build;

    private ProtonCapabilities Capabilities => EffectiveBuild?.Capabilities ?? ProtonCapabilities.Unknown;

    private bool CompatToolChanged =>
        !string.Equals(CompatTool, SavedCompatTool, StringComparison.OrdinalIgnoreCase);

    private bool HasChanges =>
        !string.Equals(Editing.Format(), Saved, StringComparison.Ordinal) ||
        UsesGlobal != SavedUsesGlobal ||
        CompatToolChanged;

    /// <summary>Whether the confirmation is open, waiting for the save to be agreed to.</summary>
    private bool IsConfirmingSave { get; set; }

    /// <summary>
    /// What the save would do beyond writing the launch options. These land in other files or in
    /// ProtonTune's own storage, so none appear in the line being previewed.
    /// </summary>
    private IReadOnlyList<string> PendingSideEffects
    {
        get
        {
            var changes = new List<string>();

            if (CompatToolChanged)
            {
                changes.Add(CompatTool.Length == 0
                    ? "Let Steam choose the Proton build, rather than the one set now."
                    : $"Run the game under {EffectiveBuild?.DisplayName ?? CompatTool}.");
            }

            if (UsesGlobal != SavedUsesGlobal)
            {
                changes.Add(UsesGlobal
                    ? "Follow the global profile from now on."
                    : "Stop following the global profile, keeping the settings it put here.");
            }

            return changes;
        }
    }

    /// <summary>Whether the reset button is waiting for a second click.</summary>
    private bool ResetPending { get; set; }

    /// <summary>Whether there is anything to reset at all.</summary>
    private bool HasAnythingToReset =>
        Saved.Length > 0 || SavedUsesGlobal;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        IsLoading = true;
        LoadError = null;
        SaveMessage = null;

        _hasChosenSection = false;

        try
        {
            Editing = await LaunchOptionsService.GetAsync(Entry.AppId);
            Saved = Editing.Format();
            SavedUsesGlobal = await Profile.IsLinkedAsync(Entry.AppId);
            UsesGlobal = SavedUsesGlobal;

            ProtonBuilds = await ProtonTools.GetCatalogueAsync();

            var selection = ProtonBuilds.SelectionFor(Entry.AppId);

            SavedCompatTool = selection.IsExplicit
                ? selection.ToolName ?? ProtonVersionEditor.InheritValue
                : ProtonVersionEditor.InheritValue;

            CompatTool = SavedCompatTool;

            await RefreshSteamStateAsync();
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
        if (_hasChosenSection || Editor is null)
        {
            return;
        }

        _hasChosenSection = true;

        Editor.SelectFirstConfiguredCategory();
        StateHasChanged();
    }

    /// <summary>
    /// Takes a change from the editor. Editing by hand stops the game following the global
    /// profile but keeps what the profile had already put there.
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
    /// Turning it off leaves the settings as they are: the profile is a starting point rather than
    /// an owner.
    /// </remarks>
    private async Task OnUseGlobalChanged(bool useGlobal)
    {
        UsesGlobal = useGlobal;
        SaveMessage = null;

        if (!useGlobal)
        {
            return;
        }

        Editing = await Profile.GetAsync();
    }

    /// <summary>
    /// Takes a change of Proton build. Unlike a launch option this does not stop the game
    /// following the global profile, which carries settings rather than a build.
    /// </summary>
    private void OnCompatToolChanged(string toolName)
    {
        CompatTool = toolName;
        SaveMessage = null;
        ResetPending = false;
    }

    private void Revert()
    {
        Editing = LaunchOptions.Parse(Saved);
        UsesGlobal = SavedUsesGlobal;
        CompatTool = SavedCompatTool;
        SaveMessage = null;
        ResetPending = false;
    }

    /// <summary>
    /// Puts the game back to how it was before ProtonTune touched it: no launch options, and not
    /// following the profile.
    /// </summary>
    /// <remarks>
    /// Asks first, since everything the game has configured goes at once. The choice of Proton
    /// build is left alone: it is as likely to have been made in Steam's own interface as here.
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
            var result = await LaunchOptionsService.SaveAsync(Entry.AppId, string.Empty);

            SaveFailed = !result.IsSuccess;

            if (result.IsSuccess)
            {
                await Profile.SetLinkedAsync(Entry.AppId, false);

                Editing = new LaunchOptions();
                Saved = string.Empty;
                UsesGlobal = false;
                SavedUsesGlobal = false;

                SaveMessage = "Reset. The game has no launch options." +
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
            await RefreshSteamStateAsync();
        }
    }

    /// <summary>Re-checks Steam's state, which can change while the dialog is open.</summary>
    private async Task RefreshSteamStateAsync() =>
        SaveMethod = await LaunchOptionsService.GetSaveMethodAsync();

    /// <summary>
    /// Opens the confirmation rather than saving, since saving closes Steam and writes to files it
    /// owns.
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
            var result = await LaunchOptionsService.SaveManyAsync(
                new Dictionary<uint, string> { [Entry.AppId] = Editing.Format() },
                CompatToolChanged
                    ? new Dictionary<uint, string> { [Entry.AppId] = CompatTool }
                    : new Dictionary<uint, string>());

            SaveFailed = !result.IsSuccess;

            if (result.IsSuccess)
            {
                Saved = Editing.Format();
                SavedCompatTool = CompatTool;

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
            IsConfirmingSave = false;
            await RefreshSteamStateAsync();
        }
    }

    private Task Close() => OnClose.InvokeAsync();

    /// <summary>
    /// Dismisses on Escape, unless a save is in flight — Steam is mid-restart — or the
    /// confirmation is open, which backs out of itself rather than taking the dialog with it.
    /// </summary>
    private Task OnKeyDown(KeyboardEventArgs args) =>
        args.Key == "Escape" && !IsSaving && !IsConfirmingSave ? Close() : Task.CompletedTask;
}
