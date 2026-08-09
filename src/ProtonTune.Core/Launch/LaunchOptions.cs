using System.Text.RegularExpressions;

namespace ProtonTune.Core.Launch;

/// <summary>
/// A Steam launch options string, split into the three things it can actually contain:
/// environment assignments, a chain of wrapper commands, and the game itself.
/// </summary>
/// <remarks>
/// <para>
/// Taking Overwatch's as an example:
/// <code>
/// PROTON_ENABLE_HDR=1 DXVK_HDR=1 mangohud taskset -c 0-7,16-23 %command%
/// └────────── Environment ───────┘└─────────── Wrapper ────────┘└ command ┘
/// </code>
/// </para>
/// <para>
/// Nothing is discarded. Assignments and wrapper tokens ProtonTune knows nothing about survive
/// parsing and reappear in <see cref="Format" /> in their original order, so editing one setting
/// can never silently drop another.
/// </para>
/// </remarks>
public sealed partial record LaunchOptions
{
    /// <summary>The literal Steam substitutes the game's own command line for.</summary>
    public const string CommandPlaceholder = "%command%";

    /// <summary>
    /// Assignments at the front of the string, in order. Only leading assignments count: once a
    /// wrapper command has been named, a later <c>NAME=value</c> is an argument to that command
    /// rather than an environment variable.
    /// </summary>
    public IReadOnlyList<EnvironmentVariable> Environment { get; init; } = [];

    /// <summary>
    /// Commands the game is launched through, in order — <c>mangohud</c>, <c>taskset -c …</c>,
    /// a custom script — as flat tokens, since each wrapper consumes its own arguments.
    /// </summary>
    public IReadOnlyList<string> Wrapper { get; init; } = [];

    /// <summary>
    /// Whether <see cref="CommandPlaceholder" /> was present. When it is absent Steam appends the
    /// options to the game's command line instead of substituting, so the distinction changes
    /// what the string means and has to be preserved.
    /// </summary>
    public bool HasCommandPlaceholder { get; init; }

    /// <summary>
    /// Arguments passed to the game: the tokens after the placeholder, or every non-assignment
    /// token when there is no placeholder.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Whether this represents an empty launch options string.</summary>
    public bool IsEmpty =>
        Environment.Count == 0 && Wrapper.Count == 0 && Arguments.Count == 0 && !HasCommandPlaceholder;

    /// <summary>
    /// Reads a launch options string. Never throws: a malformed string parses to whatever can be
    /// made of it, because the alternative is a game the user cannot inspect or repair.
    /// </summary>
    public static LaunchOptions Parse(string? launchOptions)
    {
        var tokens = ShellTokenizer.TokenizeWithSource(launchOptions ?? string.Empty);

        var environment = new List<EnvironmentVariable>();
        var index = 0;

        while (index < tokens.Count && AssignmentPattern().IsMatch(tokens[index].Text))
        {
            var (text, rawText) = tokens[index];
            var separator = text.IndexOf('=');

            environment.Add(new EnvironmentVariable(text[..separator], text[(separator + 1)..])
            {
                OriginalText = rawText
            });

            index++;
        }

        var rest = tokens.Skip(index).Select(token => token.Text).ToList();
        var placeholder = rest.IndexOf(CommandPlaceholder);

        return new LaunchOptions
        {
            Environment = environment,
            Wrapper = placeholder >= 0 ? rest[..placeholder] : [],
            HasCommandPlaceholder = placeholder >= 0,
            Arguments = placeholder >= 0 ? rest[(placeholder + 1)..] : rest
        };
    }

    /// <summary>
    /// Renders back to the string Steam stores. Parsing and formatting a conventionally spaced
    /// string returns it unchanged.
    /// </summary>
    public string Format()
    {
        var parts = new List<string>(Environment.Count + Wrapper.Count + Arguments.Count + 1);

        parts.AddRange(Environment.Select(FormatAssignment));

        parts.AddRange(Wrapper.Select(ShellTokenizer.Quote));

        if (HasCommandPlaceholder)
        {
            parts.Add(CommandPlaceholder);
        }

        parts.AddRange(Arguments.Select(ShellTokenizer.Quote));

        return string.Join(' ', parts);
    }

    /// <summary>
    /// Writes one assignment, keeping how the user spelled it where that still holds. An empty
    /// value is written bare — <c>NAME=</c> rather than <c>NAME=""</c>.
    /// </summary>
    private static string FormatAssignment(EnvironmentVariable variable)
    {
        if (variable.OriginalText is { } original &&
            ShellTokenizer.TokenizeWithSource(original) is [var only] &&
            only.Text == $"{variable.Name}={variable.Value}")
        {
            return original;
        }

        return variable.Value.Length == 0
            ? $"{variable.Name}="
            : $"{variable.Name}={ShellTokenizer.Quote(variable.Value)}";
    }

    /// <summary>Finds an assignment by name, or returns null when it is not set.</summary>
    public EnvironmentVariable? FindEnvironment(string name) =>
        Environment.FirstOrDefault(variable => string.Equals(variable.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// Matches a leading environment assignment. The name rules are the shell's: a letter or
    /// underscore, then letters, digits, or underscores.
    /// </summary>
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*=")]
    private static partial Regex AssignmentPattern();
}
