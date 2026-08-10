namespace ProtonTune.Core.Launch;

/// <summary>
/// What ProtonTune knows about one MangoHud option.
/// </summary>
/// <param name="Key">The option name as it appears in <c>MANGOHUD_CONFIG</c>.</param>
/// <param name="Label">A readable name.</param>
/// <param name="Group">The heading it is listed under.</param>
public sealed record MangoHudOptionDefinition(string Key, string Label, string Group)
{
    /// <summary>The control used to edit it.</summary>
    public SettingKind Kind { get; init; } = SettingKind.Toggle;

    /// <summary>The values offered for a <see cref="SettingKind.Choice" />.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>Example text for an empty <see cref="SettingKind.Text" /> field.</summary>
    public string? Placeholder { get; init; }

    /// <summary>What the option does, where it is not obvious from the name.</summary>
    public string? Description { get; init; }
}

/// <summary>
/// The MangoHud options ProtonTune offers as controls.
/// </summary>
/// <remarks>
/// MangoHud has well over a hundred options. This covers the ones worth reaching for while
/// tuning a game; everything else stays editable as text, so nothing is out of reach.
/// </remarks>
public static class MangoHudCatalog
{
    /// <summary>Headings the options are grouped under, in display order.</summary>
    public const string LimitsGroup = "Frame limiting";

    public const string MetricsGroup = "Metrics";
    public const string AppearanceGroup = "Appearance";

    /// <summary>The groups in the order they are shown.</summary>
    public static IReadOnlyList<string> Groups { get; } = [LimitsGroup, MetricsGroup, AppearanceGroup];

    /// <summary>Every recognised option.</summary>
    public static IReadOnlyList<MangoHudOptionDefinition> All { get; } =
    [
        // Frame limiting -----------------------------------------------------------------------
        new("fps_limit", "Frame rate limit", LimitsGroup)
        {
            Kind = SettingKind.Text,
            Placeholder = "224",
            Description = "One limit, or several separated by commas to cycle between them."
        },
        new("fps_limit_method", "Limiter method", LimitsGroup)
        {
            Kind = SettingKind.Choice,
            Choices = ["early", "late"],
            Description = "Late limits after rendering for lower latency; early is smoother."
        },
        new("vsync", "Vulkan present mode", LimitsGroup)
        {
            Kind = SettingKind.Choice,
            Choices = ["0", "1", "2", "3"],
            Description = "0 off, 1 mailbox, 2 relaxed, 3 on."
        },

        // Metrics ------------------------------------------------------------------------------
        new("fps", "Frame rate", MetricsGroup),
        new("frametime", "Frame time", MetricsGroup),
        new("frame_timing", "Frame time graph", MetricsGroup),
        new("gpu_stats", "GPU load", MetricsGroup),
        new("cpu_stats", "CPU load", MetricsGroup),
        new("gpu_temp", "GPU temperature", MetricsGroup),
        new("cpu_temp", "CPU temperature", MetricsGroup),
        new("vram", "Video memory", MetricsGroup),
        new("ram", "System memory", MetricsGroup),
        new("gpu_name", "GPU name", MetricsGroup),

        // Appearance ---------------------------------------------------------------------------
        new("preset", "Preset", AppearanceGroup)
        {
            Kind = SettingKind.Choice,
            Choices = ["0", "1", "2", "3", "4"],
            Description = "0 hides the overlay entirely; 4 shows everything."
        },
        new("position", "Position", AppearanceGroup)
        {
            Kind = SettingKind.Choice,
            Choices = ["top-left", "top-right", "bottom-left", "bottom-right", "top-center"]
        },
        new("font_size", "Font size", AppearanceGroup)
        {
            Kind = SettingKind.Text,
            Placeholder = "24"
        },
        new("background_alpha", "Background opacity", AppearanceGroup)
        {
            Kind = SettingKind.Text,
            Placeholder = "0.5",
            Description = "Between 0 and 1."
        },
        new("toggle_hud", "Toggle key", AppearanceGroup)
        {
            Kind = SettingKind.Text,
            Placeholder = "Shift_R+F12"
        },
        new("hud_compact", "Compact layout", AppearanceGroup),
        new("no_display", "Start hidden", AppearanceGroup)
        {
            Description = "Collects data but shows nothing until the toggle key is pressed."
        }
    ];

    private static readonly Dictionary<string, MangoHudOptionDefinition> ByKey =
        All.ToDictionary(definition => definition.Key, StringComparer.Ordinal);

    /// <summary>The options in a group, in display order.</summary>
    public static IReadOnlyList<MangoHudOptionDefinition> InGroup(string group) =>
        All.Where(definition => string.Equals(definition.Group, group, StringComparison.Ordinal)).ToList();

    /// <summary>Looks up an option, or returns null when it is not one ProtonTune offers.</summary>
    public static MangoHudOptionDefinition? Find(string key) => ByKey.GetValueOrDefault(key);
}
