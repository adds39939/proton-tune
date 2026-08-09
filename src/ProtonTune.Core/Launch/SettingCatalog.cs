namespace ProtonTune.Core.Launch;

/// <summary>
/// The environment variables ProtonTune recognises, and where each belongs.
/// </summary>
/// <remarks>
/// Deliberately partial. Anything absent from here still parses and is still written back — it
/// simply appears under custom variables rather than in a named section, so an unknown variable
/// costs the user presentation and never data.
/// </remarks>
public static class SettingCatalog
{
    /// <summary>Every recognised variable.</summary>
    public static IReadOnlyList<SettingDefinition> All { get; } =
    [
        // DLSS ---------------------------------------------------------------------------------
        new("PROTON_ENABLE_NVAPI", SettingCategory.Dlss, "Enable NVAPI")
        {
            Description = "Exposes NVIDIA's NVAPI to the game. DLSS does nothing without it."
        },
        new("PROTON_ENABLE_NGX_UPDATER", SettingCategory.Dlss, "Allow NGX updates")
        {
            Description = "Lets Proton keep NVIDIA's NGX runtime current."
        },
        new("DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION", SettingCategory.Dlss,
            "SR override preset")
        {
            Description = "Forces a super-resolution render preset regardless of what the game asks for."
        },
        new("DXVK_NVAPI_DRS_NGX_DLSS_RR_OVERRIDE_RENDER_PRESET_SELECTION", SettingCategory.Dlss,
            "Ray reconstruction override preset")
        {
            Description = "The same override, applied to ray reconstruction."
        },
        new("DXVK_NVAPI_SET_NGX_DEBUG_OPTIONS", SettingCategory.Dlss, "Display SR debug info")
        {
            Description = "Overlays DLSS's own debug readout, including which preset is live."
        },
        new("DXVK_NVAPI_ALLOW_OTHER_DRIVERS", SettingCategory.Dlss, "Allow non-NVIDIA drivers")
        {
            Description = "Lets DXVK-NVAPI load on drivers it would otherwise refuse."
        },

        // HDR ----------------------------------------------------------------------------------
        new("PROTON_ENABLE_HDR", SettingCategory.Hdr, "Enable HDR in Proton")
        {
            Description = "Lets Proton advertise HDR support to the game. Needs a Wayland session."
        },
        new("DXVK_HDR", SettingCategory.Hdr, "Enable HDR in DXVK")
        {
            Description = "Turns on HDR output in the D3D-to-Vulkan layer."
        },

        // Display ------------------------------------------------------------------------------
        new("PROTON_ENABLE_WAYLAND", SettingCategory.Display, "Run natively on Wayland")
        {
            Description = "Skips XWayland. Required before HDR will do anything."
        },

        // Graphics -----------------------------------------------------------------------------
        new("DXVK_FRAME_RATE", SettingCategory.Graphics, "Frame rate cap")
        {
            Description = "Limits frames per second inside DXVK. 0 leaves it uncapped."
        },
        new("DXVK_CONFIG_FILE", SettingCategory.Graphics, "DXVK config file")
        {
            Description = "Path to a dxvk.conf holding per-game renderer tweaks."
        },
        new("VKD3D_CONFIG", SettingCategory.Graphics, "VKD3D features")
        {
            Description = "Comma-separated VKD3D toggles, such as dxr for ray tracing."
        },
        new("VKD3D_FEATURE_LEVEL", SettingCategory.Graphics, "VKD3D feature level")
        {
            Description = "Caps the Direct3D 12 feature level reported to the game."
        },

        // CPU ----------------------------------------------------------------------------------
        new("PROTON_NO_ESYNC", SettingCategory.Cpu, "Disable esync")
        {
            Description = "Turns off eventfd synchronisation. Costs performance; fixes some games."
        },
        new("PROTON_NO_FSYNC", SettingCategory.Cpu, "Disable fsync")
        {
            Description = "Turns off futex synchronisation, the faster successor to esync."
        },

        // Overlay ------------------------------------------------------------------------------
        new("MANGOHUD_CONFIG", SettingCategory.Overlay, "MangoHud options")
        {
            Description = "Comma-separated MangoHud settings, such as fps_limit and its method."
        },
        new("DXVK_HUD", SettingCategory.Overlay, "DXVK HUD")
        {
            Description = "DXVK's built-in overlay — fps, frametimes, memory."
        },

        // Compatibility ------------------------------------------------------------------------
        new("WINEDLLOVERRIDES", SettingCategory.Compatibility, "DLL overrides")
        {
            Description = "Chooses between Wine's built-in DLLs and the game's own."
        },
        new("PROTON_FORCE_LARGE_ADDRESS_AWARE", SettingCategory.Compatibility, "Large address aware")
        {
            Description = "Gives 32-bit games access to more than 2 GB of address space."
        },
        new("PROTON_LOG", SettingCategory.Compatibility, "Proton logging")
        {
            Description = "Writes a debug log to the home directory. Slows the game down."
        }
    ];

    private static readonly Dictionary<string, SettingDefinition> ByVariable =
        All.ToDictionary(definition => definition.Variable, StringComparer.Ordinal);

    /// <summary>
    /// Looks up a variable, or returns <see langword="null"/> when ProtonTune has no opinion
    /// about it.
    /// </summary>
    public static SettingDefinition? Find(string variable) =>
        ByVariable.GetValueOrDefault(variable);
}
