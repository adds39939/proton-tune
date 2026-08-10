using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Per-game configuration: recognised settings as typed controls, everything else as raw text.
/// </summary>
/// <remarks>
/// The parsed options are the single source of truth. Typed controls edit them and the raw text
/// is regenerated; editing the raw text parses straight back. Both routes end at the same string,
/// which is the one shown in the preview and the one written to Steam.
/// </remarks>
public partial class GameConfigDialog : ComponentBase
{
    private const string LaunchChainSection = "Launch chain";
    private const string CustomSection = "Custom variables";
    private const string RawSection = "Raw";

    /// <summary>
    /// MangoHud's options all live in one variable, edited option by option rather than through
    /// the generic per-variable control.
    /// </summary>
    private const string MangoHudVariable = "MANGOHUD_CONFIG";

    private static readonly SettingDefinition MangoHudDefinition =
        SettingCatalog.Find(MangoHudVariable)!;

    /// <summary>Commands ProtonTune can add to the launch chain on the user's behalf.</summary>
    private const string MangoHudCommand = "mangohud";

    private const string GameModeCommand = "gamemoderun";

    [Inject]
    private ISteamLaunchOptionsService LaunchOptionsService { get; set; } = null!;

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

    /// <summary>The literal text in the raw editor, which may not yet be well formed.</summary>
    private string RawDraft { get; set; } = string.Empty;

    private bool IsLoading { get; set; } = true;

    private string? LoadError { get; set; }

    private SettingCategory? SelectedCategory { get; set; } = SettingCategory.Dlss;

    private string? SelectedSpecial { get; set; }

    private bool IsSaving { get; set; }

    private string? SaveMessage { get; set; }

    private bool SaveFailed { get; set; }

    private bool WillRestartSteam { get; set; }

    private bool GameIsRunning { get; set; }

    /// <summary>New variable being added under custom variables.</summary>
    private string NewVariableName { get; set; } = string.Empty;

    private string NewVariableValue { get; set; } = string.Empty;

    /// <summary>Exactly what would be written.</summary>
    private string Preview => Editing.Format();

    private bool HasChanges => !string.Equals(Preview, Saved, StringComparison.Ordinal);

    private IReadOnlyList<string> Warnings => LaunchOptionsValidator.Validate(Editing);

    /// <summary>Assignments with no definition. Never dropped, just ungrouped.</summary>
    private IReadOnlyList<EnvironmentVariable> CustomVariables =>
        Editing.Environment.Where(variable => SettingCatalog.Find(variable.Name) is null).ToList();

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
            RawDraft = Saved;

            // Open on the first category that has something set, so a configured game shows its
            // configuration rather than an empty section.
            SelectedCategory = SettingCategories.InDisplayOrder.FirstOrDefault(HasAnySet, SettingCategory.Dlss);
            SelectedSpecial = null;

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

    /// <summary>The recognised settings belonging to a category.</summary>
    private static IReadOnlyList<SettingDefinition> DefinitionsIn(SettingCategory category) =>
        SettingCatalog.All.Where(definition => definition.Category == category).ToList();

    /// <summary>How many of a category's settings this game has set.</summary>
    private int SetCountIn(SettingCategory category) =>
        DefinitionsIn(category).Count(definition => Editing.FindEnvironment(definition.Variable) is not null);

    private bool HasAnySet(SettingCategory category) => SetCountIn(category) > 0;

    private void SelectCategory(SettingCategory category)
    {
        SelectedCategory = category;
        SelectedSpecial = null;
    }

    private void SelectSpecial(string section)
    {
        SelectedSpecial = section;
        SelectedCategory = null;
    }

    /// <summary>
    /// Applies a change from a typed control and regenerates the raw text so both views agree.
    /// </summary>
    private void ApplySetting(SettingDefinition definition, string? value)
    {
        Editing = value is null
            ? Editing.RemoveEnvironment(definition.Variable)
            : Editing.SetEnvironment(definition.Variable, value);

        RawDraft = Editing.Format();
        SaveMessage = null;
    }

    /// <summary>Adds or removes a command from the launch chain.</summary>
    private void ApplyWrapperCommand(string command, bool present)
    {
        Editing = Editing.WithWrapperCommand(command, present);
        RawDraft = Editing.Format();
        SaveMessage = null;
    }

    /// <summary>Pins the game to a set of threads, or removes the pinning.</summary>
    private void ApplyCpuAffinity(string? mask)
    {
        Editing = Editing.WithCpuAffinity(mask);
        RawDraft = Editing.Format();
        SaveMessage = null;
    }

    private void RemoveCustomVariable(string name)
    {
        Editing = Editing.RemoveEnvironment(name);
        RawDraft = Editing.Format();
        SaveMessage = null;
    }

    private void SetCustomVariable(string name, string? value)
    {
        Editing = Editing.SetEnvironment(name, value ?? string.Empty);
        RawDraft = Editing.Format();
        SaveMessage = null;
    }

    private void AddCustomVariable()
    {
        var name = NewVariableName.Trim();

        if (name.Length == 0)
        {
            return;
        }

        Editing = Editing.SetEnvironment(name, NewVariableValue.Trim());
        RawDraft = Editing.Format();
        NewVariableName = string.Empty;
        NewVariableValue = string.Empty;
        SaveMessage = null;
    }

    /// <summary>
    /// Parses the raw editor back into the model. The editor keeps the user's literal text so
    /// their cursor is not thrown around mid-word by reformatting.
    /// </summary>
    private void OnRawInput(ChangeEventArgs args)
    {
        RawDraft = args.Value?.ToString() ?? string.Empty;
        Editing = LaunchOptions.Parse(RawDraft);
        SaveMessage = null;
    }

    private async Task RevertAsync()
    {
        Editing = LaunchOptions.Parse(Saved);
        RawDraft = Saved;
        SaveMessage = null;

        await Task.CompletedTask;
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
                RawDraft = Saved;
                Editing = LaunchOptions.Parse(Saved);

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
