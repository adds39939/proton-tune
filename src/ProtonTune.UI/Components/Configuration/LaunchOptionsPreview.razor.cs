using Microsoft.AspNetCore.Components;
using ProtonTune.Core.Launch;

namespace ProtonTune.UI.Components.Configuration;

/// <summary>
/// Shows what saving would write, with the difference from what is stored marked up.
/// </summary>
/// <remarks>
/// Used twice: alongside the editor as live feedback while settings are changed, and again in the
/// dialog that asks to confirm a save. Both need to say exactly the same thing, which is the
/// reason it is a component rather than markup in either.
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
    /// The pending string broken into what is staying, arriving, and going, so the change can be
    /// read at a glance rather than by comparing two long lines.
    /// </summary>
    private IReadOnlyList<LaunchDiffToken> Diff =>
        LaunchOptionsDiff.Compare(LaunchOptions.Parse(Saved), Options);

    /// <summary>
    /// Whether the options would leave the game with nothing set. Worth saying outright: an empty
    /// line reads as a rendering fault rather than as the change it is.
    /// </summary>
    private bool WritesNothing => Options.IsEmpty;
}
