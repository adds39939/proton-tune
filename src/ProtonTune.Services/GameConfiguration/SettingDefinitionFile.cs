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

    /// <summary>
    /// Present when the section configures a command in the launch chain rather than, or as well
    /// as, a set of variables.
    /// </summary>
    public CommandBlock? Command { get; set; }

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

        /// <summary>Hide the setting on other builds, rather than showing it greyed out.</summary>
        public bool RestrictToProtonBuild { get; set; }

        /// <summary>
        /// Present when the variable packs several settings into one string, in which case it is
        /// edited option by option rather than as text.
        /// </summary>
        public CompoundBlock? Compound { get; set; }
    }

    /// <summary>How a compound variable is packed, and which options are offered as controls.</summary>
    internal sealed class CompoundBlock
    {
        /// <summary>What sits between entries. Defaults to a comma.</summary>
        public string? Separator { get; set; }

        /// <summary>What joins a key to its value. Defaults to an equals sign.</summary>
        public string? Assignment { get; set; }

        public List<OptionGroup> Groups { get; set; } = [];
    }

    /// <summary>A set of options shown together under a heading.</summary>
    internal sealed class OptionGroup
    {
        /// <summary>The heading, which a group with no natural divisions can leave out.</summary>
        public string? Name { get; set; }

        public List<OptionEntry> Options { get; set; } = [];
    }

    /// <summary>One option within a compound variable.</summary>
    internal sealed class OptionEntry
    {
        public string? Key { get; set; }

        public string? Label { get; set; }

        public string? Description { get; set; }

        /// <summary>Defaults to a toggle, which writes the bare key as a flag.</summary>
        public string? Kind { get; set; }

        public List<string> Choices { get; set; } = [];

        public string? Placeholder { get; set; }
    }

    /// <summary>A command the section can put in the launch chain, and the flags it offers.</summary>
    internal sealed class CommandBlock
    {
        /// <summary>The command as it is written into the chain.</summary>
        public string? Name { get; set; }

        public string? Label { get; set; }

        public string? Description { get; set; }

        /// <summary>What ends the command's own arguments, such as Gamescope's <c>--</c>.</summary>
        public string? Terminator { get; set; }

        public List<FlagGroup> Groups { get; set; } = [];
    }

    /// <summary>A set of flags shown together under a heading.</summary>
    internal sealed class FlagGroup
    {
        /// <summary>The heading, which a short list with no natural divisions can leave out.</summary>
        public string? Name { get; set; }

        public List<FlagEntry> Flags { get; set; } = [];
    }

    /// <summary>One flag of a command.</summary>
    internal sealed class FlagEntry
    {
        /// <summary>The flag as it is written, leading dashes and all.</summary>
        public string? Flag { get; set; }

        public string? Label { get; set; }

        public string? Description { get; set; }

        /// <summary>Defaults to a toggle, which writes the bare flag with no value.</summary>
        public string? Kind { get; set; }

        public List<string> Choices { get; set; } = [];

        /// <summary>Other spellings recognised when a string is read.</summary>
        public List<string> Aliases { get; set; } = [];

        public string? Placeholder { get; set; }
    }
}
