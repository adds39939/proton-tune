namespace ProtonTune.Services.GameConfiguration;

/// <summary>
/// One setting definition file, as it is written on disk.
/// </summary>
/// <remarks>
/// Kept separate from the model the application uses. This shape belongs to the file format and
/// has to tolerate anything a person might type — missing fields, an unknown kind — whereas
/// <see cref="ProtonTune.Core.Launch.SettingDefinition" /> is only ever built once those have
/// been dealt with.
/// </remarks>
internal sealed class SettingDefinitionFile
{
    /// <summary>The section's stable key.</summary>
    public string? Id { get; set; }

    /// <summary>The heading shown to a person. Falls back to the identifier.</summary>
    public string? Title { get; set; }

    /// <summary>Where the section sits in the list, lowest first.</summary>
    public int Order { get; set; }

    public List<SettingEntry> Settings { get; set; } = [];

    /// <summary>One variable within a section.</summary>
    internal sealed class SettingEntry
    {
        public string? Variable { get; set; }

        public string? Label { get; set; }

        public string? Description { get; set; }

        /// <summary>toggle, choice, text, or number. Anything else is treated as text.</summary>
        public string? Kind { get; set; }

        /// <summary>The value written when a toggle is switched on.</summary>
        public string? On { get; set; }

        public List<string> Choices { get; set; } = [];

        public string? Placeholder { get; set; }

        /// <summary>Patterns naming the Proton builds this setting exists in.</summary>
        public List<string> ProtonBuilds { get; set; } = [];
    }
}
