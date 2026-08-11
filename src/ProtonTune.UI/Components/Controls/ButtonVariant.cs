namespace ProtonTune.UI.Components.Controls;

/// <summary>
/// What a button is for, which is what decides how it is painted.
/// </summary>
public enum ButtonVariant
{
    /// <summary>An ordinary action.</summary>
    Default,

    /// <summary>The action a screen exists to perform. At most one per group.</summary>
    Primary,

    /// <summary>An action that discards or overwrites something, such as a reset or a restore.</summary>
    Danger,

    /// <summary>
    /// An action beside the thing it acts on — removing one row of a list — where a full button
    /// would carry more weight than the action deserves.
    /// </summary>
    Quiet
}
