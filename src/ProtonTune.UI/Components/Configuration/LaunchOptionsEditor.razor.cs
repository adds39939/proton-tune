using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Proton;
using ProtonTune.Core.Steam;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Edits a set of launch options: recognised settings as typed controls, everything else as raw
/// text.
/// </summary>
/// <remarks>
/// Shared by the per-game dialog and the global profile. The options passed in are the single
/// source of truth: typed controls edit them and the raw text is regenerated, editing the raw text
/// parses straight back, and both routes end at the same string.
/// </remarks>
public partial class LaunchOptionsEditor : ComponentBase
{
    private const string ProtonSection = "Proton";
    private const string LaunchChainSection = "Launch chain";
    private const string CustomSection = "Custom variables";
    private const string AppliedSection = "Applied";
    private const string RawSection = "Raw";

    /// <summary>Commands ProtonTune can add to the launch chain on the user's behalf.</summary>
    private const string MangoHudCommand = "mangohud";

    private const string GameModeCommand = "gamemoderun";

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
    /// Two sources, and not equals: reading the build's own launch script is exact, so it decides
    /// where it has an opinion. The definition file speaks only for the renderer variables, whose
    /// names are assembled at runtime and never appear whole.
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
    /// What the host puts at the left of the editor's top bar, opposite the search box.
    /// </summary>
    /// <remarks>
    /// The per-game dialog's switch for following the global profile goes here, so the one row
    /// carries both things said about the whole set of settings rather than stacking two bands.
    /// The global profile passes nothing and the search sits alone.
    /// </remarks>
    [Parameter]
    public RenderFragment? Leading { get; set; }

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

    /// <summary>The literal text in the search box.</summary>
    private string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// The same, ready to match against. Held rather than derived, since a render asks it of every
    /// setting in the catalog and of every tab beside them.
    /// </summary>
    private SettingSearch _search = SettingSearch.None;

    private bool IsSearching => _search.IsActive;

    private IReadOnlyList<string> Warnings => LaunchOptionsValidator.Validate(Options);

    /// <summary>Assignments with no definition. Never dropped, just ungrouped.</summary>
    private IReadOnlyList<EnvironmentVariable> CustomVariables =>
        Options.Environment.Where(variable => Catalog.Find(variable.Name) is null).ToList();

    /// <summary>The same, narrowed to the search. Only the name is there to match on.</summary>
    private IReadOnlyList<EnvironmentVariable> ListedCustomVariables =>
        CustomVariables.Where(variable => _search.MatchesVariable(variable.Name)).ToList();

    /// <summary>
    /// Everything set, under the section each variable belongs to.
    /// </summary>
    /// <remarks>
    /// The answer to "what is actually on", which the sections cannot give: reading them means
    /// opening thirteen tabs and counting badges. Shown with the same controls as the section it
    /// came from rather than as text, so what is listed can also be changed and turned off.
    /// </remarks>
    private IReadOnlyList<SettingGroup> AppliedGroups => Catalog.Categories
        .Select(category => new SettingGroup(
            category.Title,
            DefinitionsIn(category).Where(IsSet).Where(_search.Matches).ToList()))
        .Where(group => group.Settings.Count > 0)
        .ToList();

    /// <summary>How many variables are set, recognised and unrecognised alike.</summary>
    private int AppliedCount => Options.Environment.Count;

    /// <summary>Whether anything set answers to the search, which is what marks the tab.</summary>
    private bool AppliedHasSearchHit => AppliedGroups.Count > 0 || ListedCustomVariables.Count > 0;

    private bool IsSet(SettingDefinition definition) =>
        Options.FindEnvironment(definition.Variable) is not null;

    /// <summary>
    /// Opens on a section rather than on nothing. Which sections exist comes from the definition
    /// files, so there is nothing to pick until they are read; without this the editor falls
    /// through to the raw text box.
    /// </summary>
    protected override void OnInitialized() => SelectedCategory = VisibleCategories.FirstOrDefault();

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var formatted = Options.Format();

        if (!string.Equals(formatted, _lastRendered, StringComparison.Ordinal))
        {
            RawDraft = formatted;
            _lastRendered = formatted;
        }

        if (SelectedCategory is { } selected && !HasAnythingToShow(selected))
        {
            SelectedCategory = VisibleCategories.FirstOrDefault();
        }
    }

    /// <summary>
    /// The sections worth listing. One whose every setting belongs to a build the game does not
    /// run cannot be used at all, and a tab opening onto nothing is worse than no tab.
    /// </summary>
    private IReadOnlyList<SettingCategory> VisibleCategories =>
        Catalog.Categories.Where(HasAnythingToShow).ToList();

    /// <summary>Whether a section would show anything if it were opened.</summary>
    private bool HasAnythingToShow(SettingCategory category)
    {
        if (category.Is(SettingCategoryIds.Cpu) || category.Is(SettingCategoryIds.MangoHud))
        {
            return true;
        }

        if (category.Command is not null)
        {
            return true;
        }

        return DefinitionsIn(category).Any(IsVisible);
    }

    /// <summary>The recognised settings belonging to a category.</summary>
    private IReadOnlyList<SettingDefinition> DefinitionsIn(SettingCategory category) =>
        Catalog.In(category);

    /// <summary>
    /// A category's settings, less the ones the build in force will never read and, while a search
    /// is running, the ones it does not name.
    /// </summary>
    private IEnumerable<SettingDefinition> ListedSettingsIn(SettingCategory category) =>
        DefinitionsIn(category).Where(IsVisible).Where(_search.Matches);

    /// <summary>
    /// The same, under the headings the section's file declares. Grouped after the hiding, so a
    /// heading whose settings were all hidden goes with them instead of standing over nothing.
    /// </summary>
    private IReadOnlyList<SettingGroup> ListedGroupsIn(SettingCategory category) =>
        SettingCatalog.Group(ListedSettingsIn(category));

    /// <summary>
    /// Whether a setting is worth showing at all on the build in force.
    /// </summary>
    /// <remarks>
    /// Two reasons to leave one out. A setting restricted to a family of builds is hidden
    /// elsewhere, so GE-only features do not fill a list against a build that will never read
    /// them; a setting that is a second name for one listed beside it is hidden everywhere, so the
    /// same switch is not offered twice. Neither applies when it already has a value, which would
    /// leave it invisible and unremovable outside the raw text.
    /// </remarks>
    private bool IsVisible(SettingDefinition definition) =>
        Options.FindEnvironment(definition.Variable) is not null ||
        (!definition.HideUnlessSet &&
            (!definition.RestrictToProtonBuild || definition.AppliesTo(Build)));

    /// <summary>
    /// How many of a category's settings are set, counting its command's flags alongside its
    /// variables.
    /// </summary>
    private int SetCountIn(SettingCategory category) =>
        DefinitionsIn(category)
            .Where(IsVisible)
            .Count(definition => Options.FindEnvironment(definition.Variable) is not null) +
        (category.Command is { } command ? command.AllFlags.Count(flag => Options.HasFlag(command, flag)) : 0);

    /// <summary>
    /// What to head the run a section's file declares before any group of its own.
    /// </summary>
    /// <remarks>
    /// Usually nothing, since a heading repeating the section's name says less than none. The
    /// exception is a section that is mostly a command: Gamescope's two variables listed after the
    /// compositor's flags would otherwise read as more of them.
    /// </remarks>
    private static string? UnheadedGroupName(SettingCategory category) =>
        category.Command is null ? null : "Settings";

    /// <summary>
    /// How many of a group's settings are set, shown beside its heading so a closed group cannot
    /// hide one.
    /// </summary>
    private int SetCountIn(SettingGroup group) =>
        group.Settings.Count(definition => Options.FindEnvironment(definition.Variable) is not null);

    /// <summary>
    /// Whether a section holds anything the search names, which is what marks its tab.
    /// </summary>
    /// <remarks>
    /// Every tab stays listed while a search runs, marked or not. A list that shrank as the term
    /// grew would answer "where is this setting" by taking away the only thing that could say the
    /// answer is nowhere.
    /// </remarks>
    private bool HasSearchHit(SettingCategory category) =>
        ListedSettingsIn(category).Any() ||
        (category.Command is { } command && _search.MatchesAnythingIn(command));

    /// <summary>Whether the custom variables hold anything the search names.</summary>
    private bool CustomHasSearchHit => ListedCustomVariables.Count > 0;

    /// <summary>Whether the section on show has nothing left after the search narrowed it.</summary>
    private bool SelectedIsEmptyUnderSearch =>
        IsSearching && SelectedCategory is { } selected && !HasSearchHit(selected);

    /// <summary>
    /// Whether a section's command is worth drawing. Its flags are searched alongside the
    /// variables, so Gamescope's <c>--hdr-enabled</c> is found by the same term that finds
    /// <c>DXVK_HDR</c>.
    /// </summary>
    private bool ShowsCommand(SettingCategory category) =>
        category.Command is { } command && _search.MatchesAnythingIn(command);

    /// <summary>
    /// Whether the controls that are not variables — the affinity picker, the toggles that put a
    /// command in the chain — belong on screen.
    /// </summary>
    /// <remarks>
    /// Only outside a search. A search asks which variables answer to a word, and a picker that
    /// answers to none of them left standing above the results would read as one of them.
    /// </remarks>
    private bool ShowsExtras => !IsSearching;

    private void OnSearchInput(ChangeEventArgs args)
    {
        SearchText = args.Value?.ToString() ?? string.Empty;
        _search = SettingSearch.For(SearchText);

        SelectFirstSearchHit();
    }

    /// <summary>
    /// Moves to the first section the term finds anything in, so typing lands on an answer rather
    /// than on whichever tab happened to be open.
    /// </summary>
    /// <remarks>
    /// Nothing moves where the term finds nothing at all: the section left open is then the one
    /// the person was reading, and it says plainly that it has no match.
    /// </remarks>
    private void SelectFirstSearchHit()
    {
        if (!IsSearching)
        {
            return;
        }

        if (VisibleCategories.FirstOrDefault(HasSearchHit) is { } hit)
        {
            SelectCategory(hit);
        }
        else if (CustomHasSearchHit)
        {
            SelectSpecial(CustomSection);
        }
    }

    /// <summary>Opens on the first category with something set, so a configured game shows it.</summary>
    public void SelectFirstConfiguredCategory()
    {
        var visible = VisibleCategories;

        SelectedCategory =
            visible.FirstOrDefault(category => SetCountIn(category) > 0) ??
            visible.FirstOrDefault();

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
    /// Parses the raw editor back into the model, keeping the literal text so reformatting does
    /// not move the cursor mid-word.
    /// </summary>
    private Task OnRawInput(ChangeEventArgs args)
    {
        RawDraft = args.Value?.ToString() ?? string.Empty;

        var options = LaunchOptions.Parse(RawDraft);

        _lastRendered = options.Format();

        return OptionsChanged.InvokeAsync(options);
    }
}
