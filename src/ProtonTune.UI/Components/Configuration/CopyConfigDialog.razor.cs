using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ProtonTune.Core.Launch;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Picks another game to take a configuration from.
/// </summary>
/// <remarks>
/// Only games that have something to copy are listed. A library is mostly games nobody has
/// configured, and a list of those is a list of rows that would do nothing.
/// </remarks>
public partial class CopyConfigDialog : ComponentBase
{
    [Inject]
    private ISteamLibraryService SteamLibrary { get; set; } = null!;

    [Inject]
    private ISteamLaunchOptionsService LaunchOptionsService { get; set; } = null!;

    /// <summary>The game being configured, which is the one thing not offered as a source.</summary>
    [Parameter]
    [EditorRequired]
    public required SteamLibraryEntry Entry { get; set; }

    /// <summary>Raised with the game whose configuration should be taken.</summary>
    [Parameter]
    public EventCallback<SteamLibraryEntry> OnChoose { get; set; }

    /// <summary>Raised when the dialog is dismissed without choosing.</summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    /// <summary>A game that could be copied from, and what it would bring.</summary>
    /// <param name="Entry">The game.</param>
    /// <param name="Summary">What it has set, for reading at a glance.</param>
    internal sealed record Candidate(SteamLibraryEntry Entry, string Summary);

    private IReadOnlyList<Candidate> Candidates { get; set; } = [];

    private string SearchTerm { get; set; } = string.Empty;

    private bool IsLoading { get; set; } = true;

    private string? LoadError { get; set; }

    /// <summary>The candidates the search leaves, or all of them when nothing is typed.</summary>
    private IReadOnlyList<Candidate> Matches => Candidates.Where(MatchesSearch).ToList();

    private string EmptyMessage => Candidates.Count == 0
        ? "No other game has launch options to copy."
        : "No game matches that search.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var games = (await SteamLibrary.GetInstalledAppsAsync())
                .Where(app => app.Kind == SteamAppKind.Game)
                .Where(app => app.AppId != Entry.AppId)
                .ToList();

            var options = await LaunchOptionsService.GetManyAsync(games.Select(game => game.AppId).ToList());

            Candidates = games
                .Where(game => options.TryGetValue(game.AppId, out var set) && !set.IsEmpty)
                .Select(game => new Candidate(game, Describe(options[game.AppId])))
                .ToList();
        }
        catch (Exception e)
        {
            Candidates = [];
            LoadError = $"Could not read the library: {e.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// One line on what a game's configuration amounts to, so the list can be read without opening
    /// every entry in it.
    /// </summary>
    internal static string Describe(LaunchOptions options)
    {
        var parts = new List<string>();

        if (options.Environment.Count > 0)
        {
            parts.Add($"{options.Environment.Count} {Pluralise(options.Environment.Count, "variable", "variables")}");
        }

        if (options.Wrapper.Count > 0)
        {
            parts.Add("a launch chain");
        }

        if (options.Arguments.Count > 0)
        {
            parts.Add($"{options.Arguments.Count} {Pluralise(options.Arguments.Count, "argument", "arguments")}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "Nothing set";
    }

    private static string Pluralise(int count, string singular, string plural) =>
        count == 1 ? singular : plural;

    private bool MatchesSearch(Candidate candidate)
    {
        var term = SearchTerm.Trim();

        return term.Length == 0 ||
               candidate.Entry.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
               candidate.Entry.AppId.ToString().Contains(term, StringComparison.Ordinal);
    }

    private void OnSearchInput(ChangeEventArgs args) =>
        SearchTerm = args.Value?.ToString() ?? string.Empty;

    private Task Choose(Candidate candidate) => OnChoose.InvokeAsync(candidate.Entry);

    private Task Cancel() => OnCancel.InvokeAsync();

    private Task OnKeyDown(KeyboardEventArgs args) =>
        args.Key == "Escape" ? Cancel() : Task.CompletedTask;
}
