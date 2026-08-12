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
/// <remarks>
/// Nor for <c>--hdr-enabled</c> without <c>ENABLE_GAMESCOPE_WSI</c>, for the same reason.
/// Gamescope's Vulkan layer does need that variable, but Gamescope sets it in the environment of
/// whatever it launches, so the game has it whether or not the launch options mention it.
/// </remarks>
public static class LaunchOptionsValidator
{
    /// <summary>The wrapper command MangoHud's configuration variable depends on.</summary>
    private const string MangoHudCommand = "mangohud";

    private const string GamescopeCommand = "gamescope";

    /// <summary>
    /// Gamescope's flag for drawing the MangoHud overlay itself, which reads the same
    /// configuration variable that <c>mangohud</c> does.
    /// </summary>
    private const string MangoAppFlag = "--mangoapp";

    /// <summary>Gamescope's switch for outputting HDR at all.</summary>
    private static readonly string[] HdrFlags = ["--hdr-enabled"];

    /// <summary>
    /// Gamescope's switch for expanding SDR into the HDR range. <c>getopt_long</c> takes any
    /// unambiguous abbreviation of a long flag, so the shorter spelling most guides use is a
    /// working one rather than a typo and means the same setting.
    /// </summary>
    private static readonly string[] InverseToneMappingFlags = ["--hdr-itm-enabled", "--hdr-itm-enable"];

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

        if (IsSet(options, "MANGOHUD_CONFIG") &&
            !HasWrapper(options, MangoHudCommand) &&
            !HasArgument(options, MangoAppFlag))
        {
            warnings.Add(
                "MangoHud options are set but the game is not launched through mangohud, so they " +
                "will be ignored.");
        }

        if (HasWrapper(options, GamescopeCommand) && HasWrapper(options, MangoHudCommand))
        {
            warnings.Add(
                "The game is launched through both Gamescope and mangohud. Gamescope's own advice " +
                "is to drop mangohud and use its --mangoapp flag, which draws the same overlay " +
                "from the same MANGOHUD_CONFIG and composes reliably.");
        }

        if (HasArgument(options, InverseToneMappingFlags) && !HasArgument(options, HdrFlags))
        {
            warnings.Add(
                "Gamescope is set to expand SDR into HDR but not to output HDR. Add --hdr-enabled, " +
                "or the expanded image is tone mapped straight back down to SDR.");
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

    /// <summary>
    /// Whether a flag is written anywhere in the launch chain, under any of its spellings.
    /// </summary>
    /// <remarks>
    /// Which command it belongs to is not checked. These flags are named distinctly enough that
    /// anything spelling them means them, and a rule that has to know the whole chain's shape
    /// would go quiet on the malformed strings it is most worth speaking up about.
    /// </remarks>
    private static bool HasArgument(LaunchOptions options, params string[] spellings) =>
        options.Wrapper.Any(token => spellings.Contains(token, StringComparer.Ordinal));
}
