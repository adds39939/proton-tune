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
    /// <summary>
    /// The values DXVK-NVAPI accepts for every <c>*_OVERRIDE_RENDER_PRESET_SELECTION</c> setting.
    /// </summary>
    /// <remarks>
    /// Taken from the value table inside <c>nvapi64.dll</c>, which spells them exactly like this:
    /// twenty-six single-letter presets, then <c>Default</c> and <c>Latest</c> in mixed case
    /// rather than the shouting case of the rest. The casing is reproduced verbatim because the
    /// name has to match the table for the override to take effect, and a value that does not
    /// match fails silently — the game simply runs with whatever preset it chose for itself.
    /// A game already set to something outside this list keeps it.
    /// </remarks>
    private static readonly string[] RenderPresets =
    [
        "RENDER_PRESET_Default",
        "RENDER_PRESET_Latest",
        .. Enumerable.Range('A', 26).Select(letter => $"RENDER_PRESET_{(char)letter}")
    ];

    /// <summary>Every recognised variable.</summary>
    public static IReadOnlyList<SettingDefinition> All { get; } =
    [
        // DLSS ---------------------------------------------------------------------------------
        new("PROTON_ENABLE_NVAPI", SettingCategory.Dlss, "Enable NVAPI")
        {
            Description = "Exposes NVIDIA's NVAPI to the game. Recent Proton enables it by default, so this is only needed on older versions.",
            Kind = SettingKind.Toggle
        },
        new("PROTON_ENABLE_NGX_UPDATER", SettingCategory.Dlss, "Allow NGX updates")
        {
            Description = "Lets Proton keep NVIDIA's NGX runtime current.",
            Kind = SettingKind.Toggle
        },
        new("DXVK_NVAPI_DRS_NGX_DLSS_SR_OVERRIDE_RENDER_PRESET_SELECTION", SettingCategory.Dlss,
            "SR override preset")
        {
            Description = "Forces a super-resolution render preset regardless of what the game asks for.",
            Kind = SettingKind.Choice,
            Choices = RenderPresets
        },
        new("DXVK_NVAPI_DRS_NGX_DLSS_RR_OVERRIDE_RENDER_PRESET_SELECTION", SettingCategory.Dlss,
            "RR override preset")
        {
            Description = "The same override, applied to ray reconstruction's denoiser.",
            Kind = SettingKind.Choice,
            Choices = RenderPresets
        },
        new("DXVK_NVAPI_DRS_NGX_DLSS_FG_OVERRIDE_RENDER_PRESET_SELECTION", SettingCategory.Dlss,
            "FG override preset")
        {
            Description = "The same override, applied to frame generation.",
            Kind = SettingKind.Choice,
            Choices = RenderPresets
        },
        new("DXVK_NVAPI_SET_NGX_DEBUG_OPTIONS", SettingCategory.Dlss, "Display SR debug info")
        {
            Description = "Overlays DLSS's own debug readout, including which preset is live.",
            Kind = SettingKind.Toggle,
            OnValue = "DLSSIndicator=1024"
        },
        new("DXVK_NVAPI_ALLOW_OTHER_DRIVERS", SettingCategory.Dlss, "Allow non-NVIDIA drivers")
        {
            Description = "Lets DXVK-NVAPI load on drivers it would otherwise refuse.",
            Kind = SettingKind.Toggle
        },

        // HDR ----------------------------------------------------------------------------------
        new("PROTON_ENABLE_HDR", SettingCategory.Hdr, "Enable HDR in Proton")
        {
            Description = "Lets Proton advertise HDR support to the game. Needs a Wayland session.",
            Kind = SettingKind.Toggle
        },
        new("DXVK_HDR", SettingCategory.Hdr, "Enable HDR in DXVK")
        {
            Description = "Turns on HDR output in the D3D-to-Vulkan layer.",
            Kind = SettingKind.Toggle
        },

        // Display ------------------------------------------------------------------------------
        new("PROTON_ENABLE_WAYLAND", SettingCategory.Display, "Run natively on Wayland")
        {
            Description = "Skips XWayland. Required before HDR will do anything.",
            Kind = SettingKind.Toggle
        },

        // Graphics -----------------------------------------------------------------------------
        new("DXVK_FRAME_RATE", SettingCategory.Graphics, "Frame rate cap")
        {
            Description = "Limits frames per second inside DXVK. 0 leaves it uncapped.",
            Placeholder = "144"
        },
        new("DXVK_CONFIG_FILE", SettingCategory.Graphics, "DXVK config file")
        {
            Description = "Path to a dxvk.conf holding per-game renderer tweaks.",
            Placeholder = "/home/you/dxvk.conf"
        },
        new("VKD3D_CONFIG", SettingCategory.Graphics, "VKD3D features")
        {
            Description = "Comma-separated VKD3D toggles, such as dxr for ray tracing.",
            Placeholder = "dxr"
        },
        new("DXVK_HUD", SettingCategory.Graphics, "DXVK HUD")
        {
            Description = "DXVK's own overlay, separate from MangoHud.",
            Placeholder = "fps,frametimes"
        },
        new("VKD3D_FEATURE_LEVEL", SettingCategory.Graphics, "VKD3D feature level")
        {
            Description = "Caps the Direct3D 12 feature level reported to the game.",
            Placeholder = "12_1"
        },

        // CPU ----------------------------------------------------------------------------------
        new("PROTON_NO_ESYNC", SettingCategory.Cpu, "Disable esync")
        {
            Description = "Turns off eventfd synchronisation. Costs performance; fixes some games.",
            Kind = SettingKind.Toggle
        },
        new("PROTON_NO_FSYNC", SettingCategory.Cpu, "Disable fsync")
        {
            Description = "Turns off futex synchronisation, the faster successor to esync.",
            Kind = SettingKind.Toggle
        },

        // MangoHud -----------------------------------------------------------------------------
        // Edited option by option rather than as one string, so this definition exists to name
        // the variable and place it, not to render a text box.
        new("MANGOHUD_CONFIG", SettingCategory.MangoHud, "MangoHud options")
        {
            Description = "Comma-separated MangoHud settings, such as fps_limit and its method."
        },
        // Compatibility ------------------------------------------------------------------------
        new("WINEDLLOVERRIDES", SettingCategory.Compatibility, "DLL overrides")
        {
            Description = "Chooses between Wine's built-in DLLs and the game's own.",
            Placeholder = "dxgi=n,b"
        },
        new("PROTON_FORCE_LARGE_ADDRESS_AWARE", SettingCategory.Compatibility, "Large address aware")
        {
            Description = "Gives 32-bit games access to more than 2 GB of address space.",
            Kind = SettingKind.Toggle
        },
        new("PROTON_LOG", SettingCategory.Compatibility, "Proton logging")
        {
            Description = "Writes a debug log to the home directory. Slows the game down.",
            Kind = SettingKind.Toggle
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
