using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components;

/// <summary>
/// Lists the Steam apps installed on this machine, filtered by a search term and by whether
/// compatibility tools should be shown alongside games.
/// </summary>
public partial class GameLibrary : ComponentBase
{
    private static readonly string HomeDirectory =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [Inject]
    private ISteamLibraryService SteamLibrary { get; set; } = null!;

    private IReadOnlyList<SteamLibraryEntry> Apps { get; set; } = [];

    private string SearchTerm { get; set; } = string.Empty;

    private bool ShowTools { get; set; }

    private bool IsLoading { get; set; } = true;

    private string? LoadError { get; set; }

    /// <summary>The apps matching the current search term and tool filter.</summary>
    private IReadOnlyList<SteamLibraryEntry> VisibleApps => Apps
        .Where(app => ShowTools || app.Kind == SteamAppKind.Game)
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
            var toolCount = Apps.Count - gameCount;

            return $"{gameCount} {Pluralise(gameCount, "game", "games")}, " +
                   $"{toolCount} compatibility {Pluralise(toolCount, "tool", "tools")}";
        }
    }

    private string EmptyMessage => Apps.Count == 0
        ? "No installed Steam apps were found. Is Steam installed for this user?"
        : "No games match the current filters.";

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

    private void OnShowToolsChanged(ChangeEventArgs args) => ShowTools = args.Value is true;

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

    /// <summary>
    /// Builds the placeholder tile shown in place of cover art, which Steam only caches under
    /// content-hashed filenames we cannot map back to an app id.
    /// </summary>
    private static string GetInitials(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words.Length switch
        {
            0 => "?",
            1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
            _ => $"{words[0][0]}{words[1][0]}".ToUpperInvariant()
        };
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "Unknown";
        }

        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double size = bytes;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.#} {units[unit]}";
    }

    private static string FormatLastPlayed(DateTimeOffset? lastPlayed)
    {
        if (lastPlayed is null)
        {
            return "Never";
        }

        var days = (DateTimeOffset.Now - lastPlayed.Value).Days;

        return days switch
        {
            <= 0 => "Today",
            1 => "Yesterday",
            < 30 => $"{days} days ago",
            _ => lastPlayed.Value.ToLocalTime().ToString("d MMM yyyy")
        };
    }

    /// <summary>Shortens an install path for display by collapsing the home directory to <c>~</c>.</summary>
    private static string Abbreviate(string path) =>
        !string.IsNullOrEmpty(HomeDirectory) && path.StartsWith(HomeDirectory, StringComparison.Ordinal)
            ? string.Concat("~", path.AsSpan(HomeDirectory.Length))
            : path;

    private static string Pluralise(int count, string singular, string plural) => count == 1 ? singular : plural;
}
