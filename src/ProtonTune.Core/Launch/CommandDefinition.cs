namespace ProtonTune.Core.Launch;

/// <summary>A named set of flags, shown together under a heading.</summary>
/// <param name="Name">
/// The heading, or <see langword="null"/> for a set that needs none — a command with few enough
/// flags to read at a glance is one unnamed group.
/// </param>
/// <param name="Flags">The flags in the order they should be shown.</param>
public sealed record CommandFlagGroup(string? Name, IReadOnlyList<CommandFlagDefinition> Flags);

/// <summary>
/// What ProtonTune knows about one flag of a wrapper command — <c>-W 3840</c> or
/// <c>--adaptive-sync</c> on Gamescope.
/// </summary>
/// <param name="Flag">
/// The flag as it is written on the command line, leading dashes and all. The spelling used when
/// one is added.
/// </param>
/// <param name="Label">A readable name.</param>
public sealed record CommandFlagDefinition(string Flag, string Label)
{
    /// <summary>
    /// The control used to edit it. A <see cref="SettingKind.Toggle" /> is written on its own with
    /// no value, which is what a switch means on a command line.
    /// </summary>
    public SettingKind Kind { get; init; } = SettingKind.Toggle;

    /// <summary>The values offered for a <see cref="SettingKind.Choice" />.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>Example text for an empty field.</summary>
    public string? Placeholder { get; init; }

    /// <summary>What the flag does, where it is not obvious from the name.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Other spellings of the same flag — <c>--output-width</c> for <c>-W</c>, and the
    /// abbreviations <c>getopt_long</c> accepts for a long name.
    /// </summary>
    /// <remarks>
    /// Recognised when a string is read, so a flag someone wrote out in full is edited rather than
    /// duplicated. <see cref="Flag" /> is what gets written for a new one.
    /// </remarks>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>Whether the flag is followed by a value.</summary>
    public bool TakesValue => Kind != SettingKind.Toggle;

    /// <summary>Every spelling, the one that gets written first.</summary>
    public IEnumerable<string> Spellings => Aliases.Prepend(Flag);

    /// <summary>Whether a token names this flag.</summary>
    public bool Matches(string token) => Spellings.Contains(token, StringComparer.Ordinal);
}

/// <summary>
/// A command ProtonTune can put in the chain a game is launched through, and the flags on it worth
/// setting.
/// </summary>
/// <remarks>
/// <para>
/// Declared by a setting definition file, the same as the variables beside it. Not everything worth
/// configuring is an environment variable: Gamescope is set entirely through flags, so a section
/// that could only describe variables could not describe it at all.
/// </para>
/// <para>
/// Always partial, as the variable lists are. Gamescope alone has flags for VR overlays and mura
/// compensation that nobody tuning a game will reach for, and anything not listed here still
/// survives being read and written back.
/// </para>
/// </remarks>
/// <param name="Command">The command as it is written into the chain.</param>
/// <param name="Label">A readable name for what launching through it does.</param>
public sealed record CommandDefinition(string Command, string Label)
{
    /// <summary>One line on what the command does.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// The token ending the command's own arguments — Gamescope's <c>--</c>, before which every
    /// token belongs to it and after which none does.
    /// </summary>
    /// <remarks>
    /// Null for a command that takes no arguments of its own, such as <c>mangohud</c>. Without a
    /// terminator there is no way to tell where one command's arguments stop and the next command
    /// begins, so such a command is treated as having none rather than claiming what follows it.
    /// </remarks>
    public string? Terminator { get; init; }

    /// <summary>The flags ProtonTune offers as controls.</summary>
    public IReadOnlyList<CommandFlagGroup> Groups { get; init; } = [];

    /// <summary>Every offered flag, across all groups, in display order.</summary>
    public IEnumerable<CommandFlagDefinition> AllFlags => Groups.SelectMany(group => group.Flags);
}
