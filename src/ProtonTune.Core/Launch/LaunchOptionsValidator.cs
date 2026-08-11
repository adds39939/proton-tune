namespace ProtonTune.Core.Launch;

/// <summary>
/// Checks a configuration for combinations that will not do what the user expects.
/// </summary>
/// <remarks>
/// These are warnings, never refusals. Every one of them describes a setting that is valid on its
/// own and simply has no effect as configured — the sort of thing that otherwise costs an evening
/// of wondering why nothing changed.
/// </remarks>
/// <remarks>
/// There is deliberately no warning for a DLSS override without <c>PROTON_ENABLE_NVAPI</c>.
/// Proton enables NVAPI by default now, so that combination is not merely valid but the normal
/// one, and a rule for it would fire on working configurations.
/// </remarks>
public static class LaunchOptionsValidator
{
    /// <summary>The wrapper command MangoHud's configuration variable depends on.</summary>
    private const string MangoHudCommand = "mangohud";

    /// <summary>Returns a warning for each combination worth pointing out.</summary>
    public static IReadOnlyList<string> Validate(LaunchOptions options)
    {
        var warnings = new List<string>();

        if ((IsOn(options, "PROTON_ENABLE_HDR") || IsOn(options, "DXVK_HDR")) &&
            !IsOn(options, "PROTON_ENABLE_WAYLAND"))
        {
            warnings.Add(
                "HDR is enabled but the game is not set to run natively on Wayland. HDR does " +
                "nothing through XWayland.");
        }

        if (IsSet(options, "MANGOHUD_CONFIG") && !HasWrapper(options, MangoHudCommand))
        {
            warnings.Add(
                "MangoHud options are set but the game is not launched through mangohud, so they " +
                "will be ignored.");
        }

        if (!options.HasCommandPlaceholder && (options.Environment.Count > 0 || options.Wrapper.Count > 0))
        {
            warnings.Add(
                "There is no %command% placeholder, so Steam passes all of this to the game as " +
                "arguments instead of applying it. Add %command% at the end.");
        }

        if (IsOn(options, "PROTON_NO_ESYNC") && IsOn(options, "PROTON_NO_FSYNC"))
        {
            warnings.Add(
                "Both esync and fsync are disabled. Expect noticeably worse performance unless a " +
                "specific game needs it.");
        }

        return warnings;
    }

    private static bool IsSet(LaunchOptions options, string variable) =>
        options.FindEnvironment(variable)?.Value.Length > 0;

    private static bool IsOn(LaunchOptions options, string variable) =>
        options.FindEnvironment(variable) is { Value: var value } &&
        value.Length > 0 &&
        !string.Equals(value, "0", StringComparison.Ordinal);

    /// <summary>
    /// Whether a command appears in the launch chain. Matched on the file name so an absolute
    /// path to the same tool still counts.
    /// </summary>
    private static bool HasWrapper(LaunchOptions options, string command) =>
        options.Wrapper.Any(token =>
            string.Equals(Path.GetFileName(token), command, StringComparison.Ordinal));
}
