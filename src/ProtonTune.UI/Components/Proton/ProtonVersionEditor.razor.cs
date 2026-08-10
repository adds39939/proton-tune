using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Proton;
using ProtonTune.Services.Proton;

namespace ProtonTune.UI.Components.Proton;

/// <summary>
/// Chooses which Proton build one game runs under.
/// </summary>
/// <remarks>
/// The choice is not applied here. It is held by the dialog and written with the launch options,
/// because the two live in different files that a running Steam holds in memory together — saving
/// them separately would mean closing and reopening Steam twice for one change.
/// </remarks>
public partial class ProtonVersionEditor : ComponentBase
{
    /// <summary>Value meaning "no choice of its own", which is how Steam records a cleared one.</summary>
    public const string InheritValue = "";

    [Inject]
    private IProtonToolService Tools { get; set; } = null!;

    /// <summary>The app being configured.</summary>
    [Parameter]
    [EditorRequired]
    public required uint AppId { get; set; }

    /// <summary>
    /// The chosen build's internal name, or <see cref="InheritValue" /> to leave it to Steam.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public required string Value { get; set; }

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Whether a save is in flight, which locks the control.</summary>
    [Parameter]
    public bool IsBusy { get; set; }

    private ProtonCatalogue Catalogue { get; set; } = ProtonCatalogue.Empty;

    private bool IsLoading { get; set; } = true;

    private string? LoadError { get; set; }

    /// <summary>What the game runs under now, before anything in this dialog is saved.</summary>
    private ProtonSelection Stored => Catalogue.SelectionFor(AppId);

    private ProtonSelection DefaultSelection => Catalogue.Default;

    /// <summary>The build the pending choice names, if it is installed.</summary>
    private ProtonBuild? Chosen => Catalogue.FindBuild(Value);

    private bool IsInherited => Value.Length == 0;

    /// <summary>
    /// Whether the pending choice names a build that is not installed. Only reachable when Steam
    /// already held such a mapping — the list itself offers installed builds only.
    /// </summary>
    private bool ChoiceIsMissing => !IsInherited && Chosen is null;

    /// <summary>
    /// What is stored for this game, in the same terms the control uses: a game inheriting the
    /// default has made no choice, so it reads as <see cref="InheritValue" /> rather than as the
    /// default's name.
    /// </summary>
    private string StoredValue => Stored.IsExplicit ? Stored.ToolName ?? InheritValue : InheritValue;

    private bool HasPendingChange =>
        !string.Equals(Value, StoredValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>How the build currently in force should be named in a sentence.</summary>
    private string StoredName => Stored.Build?.DisplayName ?? Stored.ToolName ?? "whatever Steam chose";

    private string StoredSummary => Stored switch
    {
        { IsExplicit: true, Build: { } build } => $"Currently set to {build.DisplayName}.",
        { IsExplicit: true, ToolName: { } toolName } => $"Currently set to {toolName}, which is not installed.",
        { Build: { } build } => $"No choice of its own, so Steam decides — normally {build.DisplayName}.",
        _ => "No choice of its own, and no default is set, so Steam decides."
    };

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Catalogue = await Tools.GetCatalogueAsync();
        }
        catch (Exception e)
        {
            LoadError = $"Could not read the installed Proton builds: {e.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Describes what Steam does with a game that has made no choice. It is not a promise: Steam
    /// applies the default only where the game's own metadata does not name a build, and that
    /// lives in a cache ProtonTune does not read.
    /// </summary>
    private string InheritLabel => DefaultSelection.Build is { } build
        ? $"Let Steam decide — usually {build.DisplayName}"
        : "Let Steam decide";

    private static string OptionLabel(ProtonBuild build) =>
        build.Kind == ProtonBuildKind.Valve ? build.DisplayName : $"{build.DisplayName} (community)";

    private Task OnSelected(ChangeEventArgs args) =>
        ValueChanged.InvokeAsync(args.Value?.ToString() ?? InheritValue);
}
