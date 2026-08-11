using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Settings;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Settings;
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

    [Inject]
    private IAppSettingsService Settings { get; set; } = null!;

    private IReadOnlyList<SteamLibraryEntry> Apps { get; set; } = [];

    private string SearchTerm { get; set; } = string.Empty;

    /// <summary>The available view modes, in the order their buttons appear.</summary>
    private static readonly LibraryViewMode[] ViewModes = Enum.GetValues<LibraryViewMode>();

    /// <summary>The available orders, in the order they appear in the menu.</summary>
    private static readonly LibrarySortOrder[] SortOrders = Enum.GetValues<LibrarySortOrder>();

    /// <summary>
    /// How the library is being shown and ordered. Both are read from the stored settings when the
    /// page opens and written back as soon as either changes, so the library reopens as it was
    /// left. Until the read finishes they hold the defaults, which is what a first run shows.
    /// </summary>
    private LibraryViewMode ViewMode { get; set; }

    private LibrarySortOrder SortOrder { get; set; }

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
    private IReadOnlyList<SteamLibraryEntry> VisibleApps => SortOrder
        .Apply(Apps.Where(app => app.Kind == SteamAppKind.Game).Where(MatchesSearch))
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
    protected override async Task OnInitializedAsync()
    {
        var settings = await Settings.GetAsync();

        ViewMode = settings.LibraryView;
        SortOrder = settings.LibrarySort;

        await LoadAsync();
    }

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

    private Task SetViewMode(LibraryViewMode mode)
    {
        ViewMode = mode;

        return RememberAsync(settings => settings with { LibraryView = mode });
    }

    private Task OnSortChanged(ChangeEventArgs args)
    {
        if (!Enum.TryParse<LibrarySortOrder>(args.Value?.ToString(), out var order))
        {
            return Task.CompletedTask;
        }

        SortOrder = order;

        return RememberAsync(settings => settings with { LibrarySort = order });
    }

    /// <summary>
    /// Writes a change back to the stored settings, reading them first so that whatever else the
    /// settings page has put there is carried through rather than overwritten.
    /// </summary>
    /// <remarks>
    /// A preference that fails to save is not worth interrupting anyone over: the library is
    /// already showing what was asked for, and the only cost is opening on the other view next
    /// time. The service logs what went wrong.
    /// </remarks>
    private async Task RememberAsync(Func<AppSettings, AppSettings> change)
    {
        try
        {
            await Settings.SaveAsync(change(await Settings.GetAsync()));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

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
