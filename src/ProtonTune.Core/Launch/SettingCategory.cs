namespace ProtonTune.Core.Launch;

/// <summary>
/// The groups a launch setting can belong to. These are how the settings are presented, not how
/// Proton organises anything — the variables themselves come from unrelated projects.
/// </summary>
public enum SettingCategory
{
    /// <summary>High dynamic range output.</summary>
    Hdr,

    /// <summary>Windowing and presentation — Wayland, resolution, upscaling.</summary>
    Display,

    /// <summary>NVIDIA DLSS and the NGX runtime behind it.</summary>
    Dlss,

    /// <summary>Processor scheduling: affinity, and the sync primitives Proton uses.</summary>
    Cpu,

    /// <summary>The MangoHud overlay and its frame limiting.</summary>
    MangoHud,

    /// <summary>Renderer behaviour in DXVK and VKD3D.</summary>
    Graphics,

    /// <summary>Wine-level workarounds and logging.</summary>
    Compatibility
}

/// <summary>Presentation details for <see cref="SettingCategory" />.</summary>
public static class SettingCategories
{
    /// <summary>The categories in the order they should be listed.</summary>
    public static IReadOnlyList<SettingCategory> InDisplayOrder { get; } =
    [
        SettingCategory.Dlss,
        SettingCategory.Hdr,
        SettingCategory.Display,
        SettingCategory.Graphics,
        SettingCategory.Cpu,
        SettingCategory.MangoHud,
        SettingCategory.Compatibility
    ];

    /// <summary>The heading shown for a category, since several are acronyms.</summary>
    public static string Title(this SettingCategory category) => category switch
    {
        SettingCategory.Hdr => "HDR",
        SettingCategory.Display => "Display",
        SettingCategory.Dlss => "DLSS",
        SettingCategory.Cpu => "CPU",
        SettingCategory.MangoHud => "MangoHud",
        SettingCategory.Graphics => "Graphics",
        SettingCategory.Compatibility => "Compatibility",
        _ => category.ToString()
    };
}
