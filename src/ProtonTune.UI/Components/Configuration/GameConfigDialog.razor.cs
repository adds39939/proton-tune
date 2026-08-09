using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Per-game configuration, read from the launch options Steam already has stored.
/// </summary>
/// <remarks>
/// Read-only for now, deliberately. Everything the parser found is shown — recognised settings
/// under their section, everything else under custom variables and the launch chain — so the
/// parse can be checked against a real game before ProtonTune is given the ability to write.
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

    /// <summary>
    /// Recognised settings that are actually set, grouped by section and ordered for display.
    /// Categories with nothing set are left out rather than shown empty.
    /// </summary>
    private IReadOnlyList<SettingSection> Sections { get; set; } = [];

    /// <summary>Assignments ProtonTune has no definition for. Never dropped, just ungrouped.</summary>
    private IReadOnlyList<EnvironmentVariable> CustomVariables =>
        Options.Environment.Where(variable => SettingCatalog.Find(variable.Name) is null).ToList();

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        IsLoading = true;
        LoadError = null;

        try
        {
            Options = await LaunchOptionsService.GetAsync(Entry.AppId);
            Sections = BuildSections(Options);
            SelectedSection = Sections.Count > 0 ? Sections[0].Title : RawSection;
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

    /// <summary>
    /// Groups the assignments this game has set into the sections that have something to show.
    /// </summary>
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

    private Task Close() => OnClose.InvokeAsync();

    /// <summary>
    /// Dismisses on Escape. The close button takes focus when the dialog opens, so the key event
    /// starts inside the panel and bubbles to the handler.
    /// </summary>
    private Task OnKeyDown(KeyboardEventArgs args) =>
        args.Key == "Escape" ? Close() : Task.CompletedTask;

    /// <summary>One named group of settings in the sidebar.</summary>
    private sealed record SettingSection(string Title, IReadOnlyList<SettingValue> Settings);

    /// <summary>A recognised setting together with the value this game has for it.</summary>
    private sealed record SettingValue(SettingDefinition Definition, string Value);
}
