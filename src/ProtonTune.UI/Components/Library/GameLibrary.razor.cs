using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components.Library;

/// <summary>
/// Lists the Steam apps installed on this machine, filtered by a search term and by whether
/// compatibility tools should be shown alongside games.
/// </summary>
public partial class GameLibrary : ComponentBase
{
    [Inject]
    private ISteamLibraryService SteamLibrary { get; set; } = null!;

    private IReadOnlyList<SteamLibraryEntry> Apps { get; set; } = [];

    private string SearchTerm { get; set; } = string.Empty;

    /// <summary>The available view modes, in the order their buttons appear.</summary>
    private static readonly LibraryViewMode[] ViewModes = Enum.GetValues<LibraryViewMode>();

    private LibraryViewMode ViewMode { get; set; } = LibraryViewMode.Grid;

    /// <summary>The entry whose configuration dialog is open, or null when none is.</summary>
    private SteamLibraryEntry? SelectedApp { get; set; }

    private bool IsLoading { get; set; } = true;

    private string? LoadError { get; set; }

    /// <summary>
    /// The games matching the current search.
    /// </summary>
    /// <remarks>
    /// Compatibility tools are never listed. Proton and the Steam runtimes are installed as apps
    /// and share the library, but they are not launched and have nothing to configure, so showing
    /// them is only ever noise.
    /// </remarks>
    private IReadOnlyList<SteamLibraryEntry> VisibleApps => Apps
        .Where(app => app.Kind == SteamAppKind.Game)
        .Where(MatchesSearch)
        .ToList();

    private string SubtitleText
    {
        get
        {
            if (IsLoading || LoadError is not null)
            {
                return "Steam games installed on this machine";
            }

            var gameCount = Apps.Count(app => app.Kind == SteamAppKind.Game);

            return $"{gameCount} {Pluralise(gameCount, "game", "games")} installed";
        }
    }

    private string EmptyMessage => Apps.Count == 0
        ? "No installed Steam apps were found. Is Steam installed for this user?"
        : "No games match that search.";

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        IsLoading = true;
        LoadError = null;

        try
        {
            Apps = await SteamLibrary.GetInstalledAppsAsync();
        }
        catch (Exception e)
        {
            Apps = [];
            LoadError = $"Could not read the Steam library: {e.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnSearchChanged(ChangeEventArgs args) => SearchTerm = args.Value?.ToString() ?? string.Empty;

    private void SetViewMode(LibraryViewMode mode) => ViewMode = mode;

    private void Select(SteamLibraryEntry entry) => SelectedApp = entry;

    private void CloseDialog() => SelectedApp = null;

    private bool MatchesSearch(SteamLibraryEntry app)
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            return true;
        }

        var term = SearchTerm.Trim();

        return app.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
               app.AppId.ToString().Contains(term, StringComparison.Ordinal);
    }

    private static string Pluralise(int count, string singular, string plural) => count == 1 ? singular : plural;
}
