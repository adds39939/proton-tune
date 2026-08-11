using System.Text;

namespace ProtonTune.Core.Launch;

/// <summary>
/// One token of a command line, both as it was written and as it means.
/// </summary>
/// <param name="Text">The logical value, with quotes and escapes resolved.</param>
/// <param name="RawText">
/// The exact source text, so a token that has not been edited can be written back untouched
/// rather than re-quoted into an equivalent but differently spelled form.
/// </param>
public readonly record struct ShellToken(string Text, string RawText);

/// <summary>
/// Splits and rebuilds command lines using the quoting rules Steam applies to launch options.
/// </summary>
/// <remarks>
/// Tokens carry both their logical value and their original spelling. Quoting in these strings is
/// often redundant — <c>WINEDLLOVERRIDES="dxgi=n,b"</c> means exactly what the unquoted form
/// means — but it is how the guides write it, so re-quoting from the logical value alone would
/// hand users back a string that differs from the one they wrote for no reason they can see.
/// </remarks>
/// <remarks>
/// Inside double quotes a backslash only escapes <c>"</c>, <c>\</c>, <c>$</c> and <c>`</c>;
/// before anything else it stands for itself.
/// </remarks>
public static class ShellTokenizer
{
    /// <summary>
    /// Characters that force a token to be quoted, because leaving them bare would change how a
    /// shell reads the command line.
    /// </summary>
    private const string MustQuote = " \t\n\r\"'\\$`;&|<>()#!*?[]{}~";

    /// <summary>
    /// Splits a command line into logical tokens, resolving quotes and backslash escapes.
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string commandLine) =>
        TokenizeWithSource(commandLine).Select(token => token.Text).ToList();

    /// <summary>
    /// Splits a command line, keeping each token's original text alongside its value.
    /// </summary>
    /// <remarks>
    /// An unterminated quote is treated as though it closed at the end of the input rather than
    /// raising: these strings come from a hand-edited Steam text box, and refusing to read a
    /// malformed one would leave the user unable to see or repair it.
    /// </remarks>
    public static IReadOnlyList<ShellToken> TokenizeWithSource(string commandLine)
    {
        var tokens = new List<ShellToken>();
        var current = new StringBuilder();
        var start = 0;
        var started = false;
        var inSingleQuotes = false;
        var inDoubleQuotes = false;

        void Begin(int index)
        {
            if (started)
            {
                return;
            }

            started = true;
            start = index;
        }

        for (var i = 0; i < commandLine.Length; i++)
        {
            var c = commandLine[i];

            if (!inSingleQuotes && !inDoubleQuotes && char.IsWhiteSpace(c))
            {
                if (started)
                {
                    tokens.Add(new ShellToken(current.ToString(), commandLine[start..i]));
                    current.Clear();
                    started = false;
                }

                continue;
            }

            Begin(i);

            if (c == '\\' && !inSingleQuotes && i + 1 < commandLine.Length)
            {
                var next = commandLine[i + 1];

                if (inDoubleQuotes && next is not ('"' or '\\' or '$' or '`'))
                {
                    current.Append(c);
                }
                else
                {
                    current.Append(next);
                    i++;
                }

                continue;
            }

            if (c == '\'' && !inDoubleQuotes)
            {
                inSingleQuotes = !inSingleQuotes;

                continue;
            }

            if (c == '"' && !inSingleQuotes)
            {
                inDoubleQuotes = !inDoubleQuotes;

                continue;
            }

            current.Append(c);
        }

        if (started)
        {
            tokens.Add(new ShellToken(current.ToString(), commandLine[start..]));
        }

        return tokens;
    }

    /// <summary>
    /// Renders a logical token back into command-line syntax, quoting only when the token would
    /// otherwise be misread.
    /// </summary>
    public static string Quote(string token)
    {
        if (token.Length == 0)
        {
            return "\"\"";
        }

        if (!token.Any(MustQuote.Contains))
        {
            return token;
        }

        var quoted = new StringBuilder(token.Length + 2).Append('"');

        foreach (var c in token)
        {
            if (c is '"' or '\\' or '$' or '`')
            {
                quoted.Append('\\');
            }

            quoted.Append(c);
        }

        return quoted.Append('"').ToString();
    }
}
