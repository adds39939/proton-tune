using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Steam;
using ProtonTune.Services.Steam;

namespace ProtonTune.UI.Components.Game;

/// <summary>
/// Cover art for an app, falling back to a lettered tile. Steam publishes no artwork for
/// compatibility tools, and a game can be missing a given shape, so the fallback is the normal
/// path rather than an error case.
/// </summary>
public partial class GameArtwork : ComponentBase
{
    [Inject]
    private IGameArtworkService Artwork { get; set; } = null!;

    /// <summary>The app whose artwork should be shown.</summary>
    [Parameter]
    [EditorRequired]
    public uint AppId { get; set; }

    /// <summary>The app's name, used to build the fallback tile.</summary>
    [Parameter]
    [EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>Which shape of artwork to request.</summary>
    [Parameter]
    public GameArtworkKind Kind { get; set; } = GameArtworkKind.Capsule;

    private string? Source { get; set; }

    private bool HasFailed { get; set; }

    private string ShapeClass => Kind == GameArtworkKind.Capsule ? "capsule" : "header";

    private string Initials => GetInitials(Name);

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        var source = await Artwork.GetArtworkSourceAsync(AppId, Kind);

        // Only reset the failure flag when the request actually changed, or a re-render after a
        // failed load would retry the same broken URL on every parameter change.
        if (source != Source)
        {
            Source = source;
            HasFailed = false;
        }
    }

    private void OnLoadFailed() => HasFailed = true;

    /// <summary>
    /// Builds the lettered tile shown when there is no artwork: two initials for a multi-word
    /// name, otherwise the first two characters.
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
}
