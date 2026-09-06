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
/// <remarks>
/// Reached by its own route rather than opened over the library, so a game being configured has an
/// address: it survives a reload, and the library is a step back rather than a dismissal.
/// </remarks>
public partial class GameConfigPanel : ComponentBase
{
    /// <summary>Where the library lives, which is what Back returns to.</summary>
    private const string LibraryRoute = "/";

    /// <summary>Steam's own address for running a game, shown so it can be read at a glance.</summary>
    private const string LaunchedMessage = "Asked Steam to launch the game.";

    [Inject]
    private ISteamLaunchOptionsService LaunchOptionsService { get; set; } = null!;

    [Inject]
    private ISteamLibraryService SteamLibrary { get; set; } = null!;

    [Inject]
    private IGlobalProfileService Profile { get; set; } = null!;

    [Inject]
    private IProtonToolService ProtonTools { get; set; } = null!;

    [Inject]
    private ISteamClient SteamClient { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>
    /// The app to configure, as the route spells it.
    /// </summary>
    /// <remarks>
    /// A <see cref="long" /> because that is the widest whole number a route constraint offers and
    /// Steam's identifiers are unsigned. Anything outside the range is a typed address rather than
    /// a game, and is answered the same way an unknown identifier is.
    /// </remarks>
    [Parameter]
    public long AppId { get; set; }

    /// <summary>
    /// The entry being configured, or <see langword="null"/> while it is being looked up and where
    /// no installed app carries the identifier.
    /// </summary>
    private SteamLibraryEntry? Entry { get; set; }

    /// <summary>The identifier the state below was loaded for, so a re-render does not reload.</summary>
    private long? _loadedAppId;

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

    /// <summary>
    /// How the status line reads the message it is showing. A launch waiting to be confirmed is a
    /// warning rather than a result, so it is not coloured as one.
    /// </summary>
    private string StatusClass => SaveFailed
        ? "status-error"
        : LaunchPending
            ? "status-warning"
            : "status-success";

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

    /// <summary>Whether the picker for copying another game's configuration is open.</summary>
    private bool IsChoosingSource { get; set; }

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

    /// <summary>
    /// Whether the launch button is waiting for a second click, having found unsaved changes.
    /// </summary>
    /// <remarks>
    /// Steam runs the game from what it has stored, so launching with edits on screen would run
    /// the settings the person can see and has not agreed to yet — silently, and looking as though
    /// it had used them.
    /// </remarks>
    private bool LaunchPending { get; set; }

    /// <summary>Whether there is anything to reset at all.</summary>
    private bool HasAnythingToReset =>
        Saved.Length > 0 || SavedUsesGlobal;

    /// <summary>
    /// Loads the game the address names, once per identifier.
    /// </summary>
    /// <remarks>
    /// Guarded on the identifier rather than run on every parameter set: a reload here would throw
    /// away edits that have not been saved, and a re-render is not a request for a different game.
    /// </remarks>
    protected override async Task OnParametersSetAsync()
    {
        if (_loadedAppId == AppId)
        {
            return;
        }

        _loadedAppId = AppId;

        IsLoading = true;
        LoadError = null;
        SaveMessage = null;

        Entry = null;
        _hasChosenSection = false;

        try
        {
            Entry = await FindEntryAsync();

            if (Entry is null)
            {
                return;
            }

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

    /// <summary>
    /// Finds the installed app the address names.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> where nothing installed carries the identifier, which includes an
    /// address outside the range Steam's identifiers occupy at all.
    /// </returns>
    private async Task<SteamLibraryEntry?> FindEntryAsync()
    {
        if (AppId is <= 0 or > uint.MaxValue)
        {
            return null;
        }

        var wanted = (uint)AppId;
        var apps = await SteamLibrary.GetInstalledAppsAsync();

        return apps.FirstOrDefault(app => app.AppId == wanted);
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
        LaunchPending = false;
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
        LaunchPending = false;
    }

    private void Revert()
    {
        Editing = LaunchOptions.Parse(Saved);
        UsesGlobal = SavedUsesGlobal;
        CompatTool = SavedCompatTool;
        SaveMessage = null;
        ResetPending = false;
        LaunchPending = false;
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
        if (Entry is not { } entry)
        {
            return;
        }

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
            var result = await LaunchOptionsService.SaveAsync(entry.AppId, string.Empty);

            SaveFailed = !result.IsSuccess;

            if (result.IsSuccess)
            {
                await Profile.SetLinkedAsync(entry.AppId, false);

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
        LaunchPending = false;
        IsConfirmingSave = true;
    }

    private void CancelSave() => IsConfirmingSave = false;

    private async Task SaveAsync()
    {
        if (Entry is not { } entry)
        {
            return;
        }

        IsSaving = true;
        SaveMessage = null;

        try
        {
            var result = await LaunchOptionsService.SaveManyAsync(
                new Dictionary<uint, string> { [entry.AppId] = Editing.Format() },
                CompatToolChanged
                    ? new Dictionary<uint, string> { [entry.AppId] = CompatTool }
                    : new Dictionary<uint, string>());

            SaveFailed = !result.IsSuccess;

            if (result.IsSuccess)
            {
                Saved = Editing.Format();
                SavedCompatTool = CompatTool;

                await Profile.SetLinkedAsync(entry.AppId, UsesGlobal);
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

    private void AskToCopy()
    {
        SaveMessage = null;
        ResetPending = false;
        LaunchPending = false;
        IsChoosingSource = true;
    }

    private void CancelCopy() => IsChoosingSource = false;

    /// <summary>
    /// Takes another game's launch options and its choice of Proton build.
    /// </summary>
    /// <remarks>
    /// Held as an unsaved change like any edit, so what arrived can be read, altered and thrown
    /// away before anything is written. Following the global profile stops, for the same reason
    /// editing by hand stops it: the settings are now this game's own.
    /// </remarks>
    private async Task CopyFrom(SteamLibraryEntry source)
    {
        IsChoosingSource = false;

        try
        {
            Editing = await LaunchOptionsService.GetAsync(source.AppId);

            var selection = ProtonBuilds.SelectionFor(source.AppId);

            CompatTool = selection.IsExplicit
                ? selection.ToolName ?? ProtonVersionEditor.InheritValue
                : ProtonVersionEditor.InheritValue;

            UsesGlobal = false;
            SaveFailed = false;
            SaveMessage = $"Copied from {source.Name}. Nothing is written until you save.";
        }
        catch (Exception e)
        {
            SaveFailed = true;
            SaveMessage = $"Could not read {source.Name}: {e.Message}";
        }
    }

    /// <summary>
    /// Hands the game to Steam, once it is clear that is what was meant.
    /// </summary>
    /// <remarks>
    /// Asks twice where there is anything unsaved, the way reset does, rather than offering to
    /// save first: which of the two was wanted is the person's to decide, and a launch that
    /// quietly saved would be a save nobody asked for.
    /// </remarks>
    private void Launch()
    {
        if (Entry is not { } entry || IsSaving)
        {
            return;
        }

        ResetPending = false;

        if (HasChanges && !LaunchPending)
        {
            LaunchPending = true;
            SaveFailed = false;
            SaveMessage = "There are unsaved changes. Launching now runs the game as Steam has it " +
                          "stored. Launch again to go ahead, or save first.";

            return;
        }

        LaunchPending = false;

        var launched = SteamClient.LaunchGame(entry.AppId);

        SaveFailed = !launched;
        SaveMessage = launched
            ? LaunchedMessage
            : "Steam could not be asked to launch the game.";
    }

    /// <summary>Returns to the library, which is where this page was reached from.</summary>
    private void Back() => Navigation.NavigateTo(LibraryRoute);

    /// <summary>
    /// Goes back on Escape, unless a save is in flight — Steam is mid-restart — or the
    /// confirmation is open, which backs out of itself rather than taking the page with it.
    /// </summary>
    private void OnKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape" && !IsSaving && !IsConfirmingSave)
        {
            Back();
        }
    }
}
