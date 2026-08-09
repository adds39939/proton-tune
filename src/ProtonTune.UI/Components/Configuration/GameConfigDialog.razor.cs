using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Per-game configuration, read from and written back to the launch options Steam has stored.
/// </summary>
/// <remarks>
/// Only the raw string is editable for now. The typed controls come later; being able to edit the
/// string directly is what exercises the whole write path, and it is the escape hatch when
/// ProtonTune does not understand a setting.
/// </remarks>
public partial class GameConfigDialog : ComponentBase
{
    /// <summary>Sections that always appear, after the setting categories.</summary>
    private const string LaunchChainSection = "Launch chain";

    private const string CustomSection = "Custom variables";
    private const string RawSection = "Raw";

    [Inject]
    private ISteamLaunchOptionsService LaunchOptionsService { get; set; } = null!;

    /// <summary>The entry being configured.</summary>
    [Parameter]
    [EditorRequired]
    public required SteamLibraryEntry Entry { get; set; }

    /// <summary>Raised when the dialog asks to be dismissed.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    private LaunchOptions Options { get; set; } = new();

    private bool IsLoading { get; set; } = true;

    private string? LoadError { get; set; }

    private string SelectedSection { get; set; } = RawSection;

    private IReadOnlyList<SettingSection> Sections { get; set; } = [];

    /// <summary>What is in the editor, which may differ from what Steam has stored.</summary>
    private string Draft { get; set; } = string.Empty;

    /// <summary>What Steam has stored, to compare the draft against.</summary>
    private string Saved { get; set; } = string.Empty;

    private bool IsSaving { get; set; }

    private string? SaveMessage { get; set; }

    private bool SaveFailed { get; set; }

    /// <summary>Whether the draft differs from what is stored.</summary>
    private bool HasChanges => !string.Equals(Draft.Trim(), Saved, StringComparison.Ordinal);

    /// <summary>
    /// What would actually be written: the draft after a parse and format, which is what any
    /// later typed editing would produce too. Showing this rather than the raw text means the
    /// user approves the exact string, not an approximation of it.
    /// </summary>
    private string Preview => LaunchOptions.Parse(Draft).Format();

    /// <summary>Whether saving now would close and reopen Steam.</summary>
    private bool WillRestartSteam { get; set; }

    /// <summary>Whether a game is running, which blocks saving entirely.</summary>
    private bool GameIsRunning { get; set; }

    private IReadOnlyList<EnvironmentVariable> CustomVariables =>
        Options.Environment.Where(variable => SettingCatalog.Find(variable.Name) is null).ToList();

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        IsLoading = true;
        LoadError = null;
        SaveMessage = null;

        try
        {
            Options = await LaunchOptionsService.GetAsync(Entry.AppId);
            Saved = Options.Format();
            Draft = Saved;
            Sections = BuildSections(Options);
            SelectedSection = Sections.Count > 0 ? Sections[0].Title : RawSection;

            RefreshSteamState();
        }
        catch (Exception e)
        {
            Options = new LaunchOptions();
            Sections = [];
            LoadError = $"Could not read launch options: {e.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static IReadOnlyList<SettingSection> BuildSections(LaunchOptions options)
    {
        var known = options.Environment
            .Select(variable => (Variable: variable, Definition: SettingCatalog.Find(variable.Name)))
            .Where(pair => pair.Definition is not null)
            .ToList();

        return SettingCategories.InDisplayOrder
            .Select(category => new SettingSection(
                category.Title(),
                known.Where(pair => pair.Definition!.Category == category)
                    .Select(pair => new SettingValue(pair.Definition!, pair.Variable.Value))
                    .ToList()))
            .Where(section => section.Settings.Count > 0)
            .ToList();
    }

    private void SelectSection(string section) => SelectedSection = section;

    private void OnDraftChanged(ChangeEventArgs args)
    {
        Draft = args.Value?.ToString() ?? string.Empty;
        SaveMessage = null;
    }

    private void RevertDraft()
    {
        Draft = Saved;
        SaveMessage = null;
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
            var result = await LaunchOptionsService.SaveAsync(Entry.AppId, Preview);

            SaveFailed = !result.IsSuccess;

            if (result.IsSuccess)
            {
                Saved = Preview;
                Draft = Preview;
                Options = LaunchOptions.Parse(Saved);
                Sections = BuildSections(Options);

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

    /// <summary>One named group of settings in the sidebar.</summary>
    private sealed record SettingSection(string Title, IReadOnlyList<SettingValue> Settings);

    /// <summary>A recognised setting together with the value this game has for it.</summary>
    private sealed record SettingValue(SettingDefinition Definition, string Value);
}
