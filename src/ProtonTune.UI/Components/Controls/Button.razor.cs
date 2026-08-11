using Microsoft.AspNetCore.Components;

namespace ProtonTune.UI.Components.Controls;

/// <summary>
/// Every button in the application.
/// </summary>
/// <remarks>
/// Exists so that the states a button can be in — hovered, disabled, primary, dangerous — are
/// decided once. The styles were previously copied into each panel that had a button, and had
/// drifted: a disabled primary button repainted itself on hover in one place and not in another.
/// </remarks>
public partial class Button : ComponentBase
{
    /// <summary>The label, which is markup rather than text so a button can carry an icon.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>What the button is for.</summary>
    [Parameter]
    public ButtonVariant Variant { get; set; }

    /// <summary>How much room it takes.</summary>
    [Parameter]
    public ButtonSize Size { get; set; }

    /// <summary>
    /// Whether the action is unavailable. A disabled button drops its variant colouring and shows
    /// no hover response, so an unavailable action never draws the eye or invites a click.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Whether the button takes focus when it first renders.</summary>
    [Parameter]
    public bool Autofocus { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }

    /// <summary>
    /// Anything else the caller sets — <c>title</c>, <c>aria-label</c>, a data attribute.
    /// </summary>
    /// <remarks>
    /// Splatted after the button's own attributes, so a caller that passes <c>class</c> replaces
    /// the styling rather than adding to it. Use <see cref="Variant" /> and <see cref="Size" />.
    /// </remarks>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? Attributes { get; set; }

    private string VariantClass => Variant switch
    {
        ButtonVariant.Primary => "button-primary",
        ButtonVariant.Danger => "button-danger",
        ButtonVariant.Quiet => "button-quiet",
        _ => string.Empty
    };

    private string SizeClass => Size == ButtonSize.Small ? "button-small" : string.Empty;
}
