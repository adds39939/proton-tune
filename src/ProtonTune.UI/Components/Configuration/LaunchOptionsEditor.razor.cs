using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Proton;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Dlss;

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
    private const string ProtonSection = "Proton";
    private const string LaunchChainSection = "Launch chain";
    private const string CustomSection = "Custom variables";
    private const string RawSection = "Raw";

    /// <summary>Commands ProtonTune can add to the launch chain on the user's behalf.</summary>
    private const string MangoHudCommand = "mangohud";

    private const string GameModeCommand = "gamemoderun";

    [Inject]
    private IDlssManagementService Dlss { get; set; } = null!;

    /// <summary>The settings on offer, read from the definition files at startup.</summary>
    [Inject]
    private SettingCatalog Catalog { get; set; } = null!;

    /// <summary>The options being edited.</summary>
    [Parameter]
    [EditorRequired]
    public required LaunchOptions Options { get; set; }

    /// <summary>Raised with the options after any change.</summary>
    [Parameter]
    public EventCallback<LaunchOptions> OptionsChanged { get; set; }

    /// <summary>
    /// The game being configured, or <see langword="null"/> when editing the global profile.
    /// Sections that only make sense for a real install — the DLSS libraries — are hidden without
    /// one.
    /// </summary>
    [Parameter]
    public SteamLibraryEntry? Entry { get; set; }

    /// <summary>
    /// The Proton build the game is set to run under, empty when it has no choice of its own.
    /// Only meaningful alongside <see cref="Entry" />; the global profile has no single game to
    /// point anywhere.
    /// </summary>
    [Parameter]
    public string CompatTool { get; set; } = string.Empty;

    /// <summary>Raised when a different build is picked.</summary>
    [Parameter]
    public EventCallback<string> CompatToolChanged { get; set; }

    /// <summary>
    /// The Proton build in force, so settings it does nothing with can say so. Null for the global
    /// profile, which is tied to no build and therefore judges nothing.
    /// </summary>
    [Parameter]
    public ProtonBuild? Build { get; set; }

    private ProtonCapabilities Capabilities => Build?.Capabilities ?? ProtonCapabilities.Unknown;

    private string? BuildName => Build?.DisplayName;

    /// <summary>
    /// Whether the build in force does nothing with a setting.
    /// </summary>
    /// <remarks>
    /// Two sources, and they are not equals. Reading the build's own launch script is exact, so
    /// where it has an opinion it decides. The definition file's declaration speaks only where it
    /// cannot — the renderer variables, whose names are assembled at runtime and never appear
    /// whole in the shipped libraries.
    /// </remarks>
    private bool IsIgnored(SettingDefinition definition) => Capabilities.Reads(definition.Variable) switch
    {
        true => false,
        false => true,
        null => !definition.AppliesTo(Build)
    };

    /// <summary>Whether a save is in flight, which locks the raw editor.</summary>
    [Parameter]
    public bool IsBusy { get; set; }

    /// <summary>
    /// The section on show. Set on first render rather than here: which sections exist is decided
    /// by the definition files, so there is no section to name until they have been read.
    /// </summary>
    private SettingCategory? SelectedCategory { get; set; }

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

    private IReadOnlyList<string> Warnings => LaunchOptionsValidator.Validate(Options);

    /// <summary>Assignments with no definition. Never dropped, just ungrouped.</summary>
    private IReadOnlyList<EnvironmentVariable> CustomVariables =>
        Options.Environment.Where(variable => Catalog.Find(variable.Name) is null).ToList();

    /// <summary>
    /// Opens on a section rather than on nothing. Which sections exist comes from the definition
    /// files, so there is nothing to pick until they have been read — and without this the editor
    /// renders with no section selected, which falls through to the raw text box.
    /// </summary>
    protected override void OnInitialized() => SelectedCategory = Catalog.Categories.FirstOrDefault();

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
    private IReadOnlyList<SettingDefinition> DefinitionsIn(SettingCategory category) =>
        Catalog.In(category);

    /// <summary>
    /// A category's settings as the generic list renders them. For a game, Proton's own DLSS
    /// settings are held back: they belong beside the libraries they replace, not among the driver
    /// overrides. The global profile has no libraries section to put them next to, so it lists
    /// them here rather than losing them.
    /// </summary>
    private IEnumerable<SettingDefinition> ListedSettingsIn(SettingCategory category) =>
        (category.Is(SettingCategoryIds.Dlss) && Entry is not null
            ? DefinitionsIn(category).Where(definition => !IsProtonDlss(definition))
            : DefinitionsIn(category))
        .Where(IsVisible);

    private IReadOnlyList<SettingDefinition> ProtonDlssSettings =>
        Catalog.All.Where(IsProtonDlss).Where(IsVisible).ToList();

    /// <summary>
    /// Whether a setting is worth showing at all on the build in force.
    /// </summary>
    /// <remarks>
    /// A setting the definition files restrict to a family of builds is hidden elsewhere, so the
    /// GE-only features do not fill a list against a build that will never read them. Never when
    /// it already has a value, though: hiding one that is set would leave it invisible and
    /// unremovable except by editing the raw text.
    /// </remarks>
    private bool IsVisible(SettingDefinition definition) =>
        !definition.RestrictToProtonBuild ||
        definition.AppliesTo(Build) ||
        Options.FindEnvironment(definition.Variable) is not null;

    private static bool IsProtonDlss(SettingDefinition definition) =>
        definition.Variable.StartsWith("PROTON_DLSS_", StringComparison.Ordinal);

    /// <summary>
    /// Whether the build replaces DLSS libraries itself, in which case ProtonTune's own swap is
    /// not offered.
    /// </summary>
    /// <remarks>
    /// Two mechanisms replacing the same files would fight: ProtonTune symlinks its shipped
    /// libraries over the game's and adds a script to put them back after Steam verifies, while
    /// Proton substitutes its own at launch without touching the install. Whichever ran last
    /// would win, and which that was would not be visible from here.
    /// </remarks>
    private bool BuildUpgradesDlss => Capabilities.Reads("PROTON_DLSS_UPGRADE") is true;

    /// <summary>Whether ProtonTune's swapped libraries are still in place inside the install.</summary>
    private bool DlssIsManaged => Entry is not null && Dlss.Inspect(Entry).HasManagedLinks;

    /// <summary>How many of a category's settings are set.</summary>
    private int SetCountIn(SettingCategory category) =>
        DefinitionsIn(category)
            .Where(IsVisible)
            .Count(definition => Options.FindEnvironment(definition.Variable) is not null);

    /// <summary>Opens on the first category with something set, so a configured game shows it.</summary>
    public void SelectFirstConfiguredCategory()
    {
        SelectedCategory =
            Catalog.Categories.FirstOrDefault(category => SetCountIn(category) > 0) ??
            Catalog.Categories.FirstOrDefault();

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
