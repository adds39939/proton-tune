using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Steam;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Edits a set of launch options: recognised settings as typed controls, everything else as raw
/// text.
/// </summary>
/// <remarks>
/// Shared by the per-game dialog and the global profile, which offer the same settings and differ
/// only in what they are attached to. The options passed in are the single source of truth: typed
/// controls edit them and the raw text is regenerated, editing the raw text parses straight back,
/// and both routes end at the same string.
/// </remarks>
public partial class LaunchOptionsEditor : ComponentBase
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

    /// <summary>The options being edited.</summary>
    [Parameter]
    [EditorRequired]
    public required LaunchOptions Options { get; set; }

    /// <summary>Raised with the options after any change.</summary>
    [Parameter]
    public EventCallback<LaunchOptions> OptionsChanged { get; set; }

    /// <summary>What is currently stored, so the preview can show what a save would change.</summary>
    [Parameter]
    public string Saved { get; set; } = string.Empty;

    /// <summary>
    /// The game being configured, or <see langword="null"/> when editing the global profile.
    /// Sections that only make sense for a real install — the DLSS libraries — are hidden without
    /// one.
    /// </summary>
    [Parameter]
    public SteamLibraryEntry? Entry { get; set; }

    /// <summary>Whether a save is in flight, which locks the raw editor.</summary>
    [Parameter]
    public bool IsBusy { get; set; }

    private SettingCategory? SelectedCategory { get; set; } = SettingCategory.Dlss;

    private string? SelectedSpecial { get; set; }

    /// <summary>The literal text in the raw editor, which may not yet be well formed.</summary>
    private string RawDraft { get; set; } = string.Empty;

    /// <summary>
    /// The last string this editor produced. Used to tell an edit of its own apart from the
    /// options being replaced from outside, which happens when a global profile is applied.
    /// </summary>
    private string _lastRendered = string.Empty;

    /// <summary>New variable being added under custom variables.</summary>
    private string NewVariableName { get; set; } = string.Empty;

    private string NewVariableValue { get; set; } = string.Empty;

    private string Preview => Options.Format();

    private bool HasChanges => !string.Equals(Preview, Saved, StringComparison.Ordinal);

    private IReadOnlyList<string> Warnings => LaunchOptionsValidator.Validate(Options);

    /// <summary>
    /// The pending string broken into what is staying, arriving, and going, so the change can be
    /// read at a glance rather than by comparing two long lines.
    /// </summary>
    private IReadOnlyList<LaunchDiffToken> PreviewDiff =>
        LaunchOptionsDiff.Compare(LaunchOptions.Parse(Saved), Options);

    /// <summary>Assignments with no definition. Never dropped, just ungrouped.</summary>
    private IReadOnlyList<EnvironmentVariable> CustomVariables =>
        Options.Environment.Where(variable => SettingCatalog.Find(variable.Name) is null).ToList();

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var formatted = Options.Format();

        // Only resync the raw text when the options were changed from outside. Doing it on every
        // render would overwrite what the user is part way through typing.
        if (!string.Equals(formatted, _lastRendered, StringComparison.Ordinal))
        {
            RawDraft = formatted;
            _lastRendered = formatted;
        }
    }

    /// <summary>The recognised settings belonging to a category.</summary>
    private static IReadOnlyList<SettingDefinition> DefinitionsIn(SettingCategory category) =>
        SettingCatalog.All.Where(definition => definition.Category == category).ToList();

    /// <summary>How many of a category's settings are set.</summary>
    private int SetCountIn(SettingCategory category) =>
        DefinitionsIn(category).Count(definition => Options.FindEnvironment(definition.Variable) is not null);

    /// <summary>Opens on the first category with something set, so a configured game shows it.</summary>
    public void SelectFirstConfiguredCategory()
    {
        SelectedCategory = SettingCategories.InDisplayOrder
            .FirstOrDefault(category => SetCountIn(category) > 0, SettingCategory.Dlss);

        SelectedSpecial = null;
    }

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

    /// <summary>Publishes a change, keeping the raw text in step with it.</summary>
    private Task Publish(LaunchOptions options)
    {
        _lastRendered = options.Format();
        RawDraft = _lastRendered;

        return OptionsChanged.InvokeAsync(options);
    }

    private Task ApplySetting(SettingDefinition definition, string? value) =>
        Publish(value is null
            ? Options.RemoveEnvironment(definition.Variable)
            : Options.SetEnvironment(definition.Variable, value));

    /// <summary>Adds or removes a command from the launch chain.</summary>
    private Task ApplyWrapperCommand(string command, bool present) =>
        Publish(Options.WithWrapperCommand(command, present));

    /// <summary>
    /// Adds or removes the generated DLSS launch script. The libraries themselves have already
    /// been changed on disk by the time this runs; the launch options entry still needs saving.
    /// </summary>
    private Task OnDlssScriptChanged((string ScriptPath, bool Present) change) =>
        ApplyWrapperCommand(change.ScriptPath, change.Present);

    /// <summary>Pins the game to a set of threads, or removes the pinning.</summary>
    private Task ApplyCpuAffinity(string? mask) => Publish(Options.WithCpuAffinity(mask));

    private Task RemoveCustomVariable(string name) => Publish(Options.RemoveEnvironment(name));

    private Task SetCustomVariable(string name, string? value) =>
        Publish(Options.SetEnvironment(name, value ?? string.Empty));

    private Task AddCustomVariable()
    {
        var name = NewVariableName.Trim();

        if (name.Length == 0)
        {
            return Task.CompletedTask;
        }

        var options = Options.SetEnvironment(name, NewVariableValue.Trim());

        NewVariableName = string.Empty;
        NewVariableValue = string.Empty;

        return Publish(options);
    }

    /// <summary>
    /// Parses the raw editor back into the model. The editor keeps the user's literal text so
    /// their cursor is not thrown around mid-word by reformatting.
    /// </summary>
    private Task OnRawInput(ChangeEventArgs args)
    {
        RawDraft = args.Value?.ToString() ?? string.Empty;

        var options = LaunchOptions.Parse(RawDraft);

        _lastRendered = options.Format();

        return OptionsChanged.InvokeAsync(options);
    }
}
