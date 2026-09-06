using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Launch;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Shows what saving would write, with the difference from what is stored marked up.
/// </summary>
/// <remarks>
/// Used twice — as live feedback beside the editor, and in the save confirmation — which is why it
/// is a component rather than markup in either.
/// </remarks>
public partial class LaunchOptionsPreview : ComponentBase
{
    /// <summary>The options as they now stand.</summary>
    [Parameter]
    [EditorRequired]
    public required LaunchOptions Options { get; set; }

    /// <summary>What Steam has stored, which the options are compared against.</summary>
    [Parameter]
    public string Saved { get; set; } = string.Empty;

    /// <summary>The heading above the line, which differs between live and confirming.</summary>
    [Parameter]
    public string Label { get; set; } = "Will be written as";

    /// <summary>
    /// The pending string broken into what is staying, arriving, and going.
    /// </summary>
    private IReadOnlyList<LaunchDiffToken> Diff =>
        LaunchOptionsDiff.Compare(LaunchOptions.Parse(Saved), Options);

    /// <summary>
    /// Whether the options would leave the game with nothing set, which is said outright so an
    /// empty line does not read as a rendering fault.
    /// </summary>
    private bool WritesNothing => Options.IsEmpty;
}
