namespace ProtonTune.Core.Launch;

/// <summary>
/// A term typed into the configuration screen's search box, and what it finds.
/// </summary>
/// <remarks>
/// <para>
/// One term is matched against everything a person could reasonably remember about a setting: the
/// variable as it is written, the name given to it, and the sentence describing it. Someone who
/// knows the variable and someone who only knows what it does arrive at the same row.
/// </para>
/// <para>
/// Matching is case insensitive and unanchored, so <c>wayland</c> finds
/// <c>PROTON_ENABLE_WAYLAND</c> and <c>hdr</c> finds every setting whose description mentions it.
/// </para>
/// </remarks>
public readonly record struct SettingSearch
{
    private SettingSearch(string term) => Term = term;

    /// <summary>The empty search, which narrows nothing.</summary>
    public static SettingSearch None => default;

    /// <summary>
    /// Reads what was typed, dropping the whitespace around it so a trailing space while typing
    /// does not empty the results.
    /// </summary>
    public static SettingSearch For(string? term) => new(term?.Trim() ?? string.Empty);

    /// <summary>What is being searched for, trimmed.</summary>
    public string Term { get; }

    /// <summary>
    /// Whether anything was typed. An inactive search matches everything, so the same code path
    /// serves the filtered and unfiltered views.
    /// </summary>
    public bool IsActive => Term is { Length: > 0 };

    /// <summary>Whether a setting is one the term names.</summary>
    public bool Matches(SettingDefinition definition) =>
        !IsActive ||
        Contains(definition.Variable) ||
        Contains(definition.Label) ||
        Contains(definition.Description);

    /// <summary>
    /// Whether a wrapper command's flag is one the term names, under any of its spellings.
    /// </summary>
    /// <remarks>
    /// The aliases are searched as well as the written spelling, so <c>--output-width</c> finds
    /// the flag ProtonTune writes as <c>-W</c>.
    /// </remarks>
    public bool Matches(CommandFlagDefinition flag) =>
        !IsActive ||
        flag.Spellings.Any(Contains) ||
        Contains(flag.Label) ||
        Contains(flag.Description);

    /// <summary>Whether a wrapper command itself is one the term names.</summary>
    public bool Matches(CommandDefinition command) =>
        !IsActive ||
        Contains(command.Command) ||
        Contains(command.Label) ||
        Contains(command.Description);

    /// <summary>
    /// Whether a variable ProtonTune has no definition for is one the term names. Its name is all
    /// there is to go on.
    /// </summary>
    public bool MatchesVariable(string name) => !IsActive || Contains(name);

    /// <summary>Whether a command or any of its flags is named, which is what puts it on screen.</summary>
    public bool MatchesAnythingIn(CommandDefinition command) =>
        Matches(command) || command.AllFlags.Any(Matches);

    private bool Contains(string? text) =>
        text is not null && text.Contains(Term, StringComparison.OrdinalIgnoreCase);
}
