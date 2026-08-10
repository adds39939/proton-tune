using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Dlss;
using ProtonTune.Core.Proton;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Dlss;
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
    private IDlssManagementService Dlss { get; set; } = null!;

    [Inject]
    private IDlssRuntimeProvider DlssRuntimes { get; set; } = null!;

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

    private bool WillRestartSteam { get; set; }

    private bool GameIsRunning { get; set; }

    /// <summary>Whether this game is following the global profile.</summary>
    private bool UsesGlobal { get; set; }

    /// <summary>What was stored, so an unchanged link is not rewritten on save.</summary>
    private bool SavedUsesGlobal { get; set; }

    /// <summary>
    /// The Proton build the game is pointed at, empty when it has none of its own. Held here
    /// rather than applied on selection so it is written in the same trip through Steam as the
    /// launch options — the two are separate files, but one shutdown.
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
    /// re-judges every setting. Choosing GE-Proton is often the answer to "why does this setting
    /// do nothing", and it would be a poor answer if the screen only agreed after saving.
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
    /// What the save would do beyond writing the launch options, so the confirmation can account
    /// for all of it. These land in other files, or in ProtonTune's own storage, and none of them
    /// show up in the line being previewed.
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
        Saved.Length > 0 || SavedUsesGlobal || Dlss.Inspect(Entry).HasManagedLinks;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        IsLoading = true;
        LoadError = null;
        SaveMessage = null;

        // A different game deserves its own opening section, worked out once its settings are read.
        _hasChosenSection = false;

        try
        {
            Editing = await LaunchOptionsService.GetAsync(Entry.AppId);
            Saved = Editing.Format();
            SavedUsesGlobal = await Profile.IsLinkedAsync(Entry.AppId);
            UsesGlobal = SavedUsesGlobal;

            // Only a choice the game has made of its own counts. Inheriting the default is not a
            // choice, and recording it as one would write a mapping the user never asked for.
            ProtonBuilds = await ProtonTools.GetCatalogueAsync();

            var selection = ProtonBuilds.SelectionFor(Entry.AppId);

            SavedCompatTool = selection.IsExplicit
                ? selection.ToolName ?? ProtonVersionEditor.InheritValue
                : ProtonVersionEditor.InheritValue;

            CompatTool = SavedCompatTool;

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
        // Not on the first render: the dialog is still showing the loading message then, so there
        // is no editor to tell. Waiting for the render that has one is what makes this land.
        if (_hasChosenSection || Editor is null)
        {
            return;
        }

        _hasChosenSection = true;

        Editor.SelectFirstConfiguredCategory();
        StateHasChanged();
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

    /// <summary>
    /// Takes a change of Proton build. Unlike a launch option this does not stop the game
    /// following the global profile: the profile carries settings, not a build, so the two do not
    /// contradict each other.
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
    /// Puts the game back to how it was before ProtonTune touched it: no launch options, not
    /// following the profile, and its own DLSS libraries restored.
    /// </summary>
    /// <remarks>
    /// Asks first. This clears settings the user cannot see from here — the DLSS libraries in
    /// particular are files inside the install — so a single mis-click should not do it.
    /// </remarks>
    /// <remarks>
    /// The choice of Proton build is deliberately left alone. It is just as likely to have been
    /// made in Steam's own interface as here, and undoing someone's Steam setting is not what
    /// resetting ProtonTune's own changes should mean.
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
            // either extreme. Run unconditionally — asking whether the game looks managed first
            // meant a half-swapped install, or one whose backup had gone, was skipped silently.
            var reverted = await Dlss.RevertAsync(Entry);

            var result = await LaunchOptionsService.SaveAsync(Entry.AppId, string.Empty);

            SaveFailed = !result.IsSuccess;

            if (result.IsSuccess)
            {
                await Profile.SetLinkedAsync(Entry.AppId, false);

                Editing = new LaunchOptions();
                Saved = string.Empty;
                UsesGlobal = false;
                SavedUsesGlobal = false;

                SaveMessage = ResetMessage(reverted) +
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

    /// <summary>
    /// Says what the reset actually did to the libraries, rather than assuming it did everything.
    /// </summary>
    /// <remarks>
    /// A library whose backup has gone cannot be put back as the game shipped it, and only Steam
    /// can supply that file again. Saying so is the difference between a reset the user can trust
    /// and one that quietly leaves a file inside the install pointing at ProtonTune.
    /// </remarks>
    private static string ResetMessage(DlssRevertResult reverted) => reverted switch
    {
        { Replaced.Count: > 0 } => $"Reset, but {string.Join(", ", reverted.Replaced)} could not be " +
                                   "put back as the game shipped it — no backup was left. A copy of the " +
                                   "version ProtonTune had linked in is in its place. Verify the game in " +
                                   "Steam to get the original file back.",
        { Restored.Count: > 0 } => "Reset. The game has no launch options and its own DLSS libraries.",
        _ => "Reset. The game has no launch options."
    };

    /// <summary>Re-checks Steam's state, which can change while the dialog is open.</summary>
    private void RefreshSteamState()
    {
        GameIsRunning = LaunchOptionsService.IsGameRunning();
        WillRestartSteam = LaunchOptionsService.RequiresSteamRestart();
    }

    /// <summary>
    /// Opens the confirmation rather than saving. Saving closes Steam and writes to files it owns,
    /// which is worth showing in full before it happens rather than explaining afterwards.
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
            // The build is only sent when it changed, so saving a setting never rewrites a mapping
            // Steam owns — including one made in Steam's own interface.
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
            RefreshSteamState();
        }
    }

    private Task Close() => OnClose.InvokeAsync();

    /// <summary>
    /// Dismisses on Escape, unless a save is in flight — Steam is mid-restart at that point and
    /// closing the dialog would hide what is happening — or the confirmation is open, which backs
    /// out of itself rather than taking the dialog with it.
    /// </summary>
    private Task OnKeyDown(KeyboardEventArgs args) =>
        args.Key == "Escape" && !IsSaving && !IsConfirmingSave ? Close() : Task.CompletedTask;
}
