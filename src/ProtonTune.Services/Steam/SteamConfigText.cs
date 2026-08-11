using System.Text;

namespace ProtonTune.Services.Steam;

/// <summary>
/// Reads and edits single values inside a KeyValues document at the character level, leaving
/// every other byte of the file exactly as it was.
/// </summary>
/// <remarks>
/// <para>
/// The obvious approach — parse to an object model, change one value, serialise the whole thing
/// back — is not safe for <c>localconfig.vdf</c>. That file holds the entire Steam client
/// configuration, including large cached JSON blobs full of escaped quotes. Re-serialising it
/// would rewrite all of them, and any difference in how the writer escapes a character would
/// corrupt settings that have nothing to do with ProtonTune.
/// </para>
/// <para>
/// Splicing one value keeps the blast radius to the string being changed. Everything else,
/// including formatting and anything this code does not understand, is copied through untouched.
/// </para>
/// </remarks>
public static class SteamConfigText
{
    /// <summary>Steam writes these files with tab indentation, one tab per level.</summary>
    private const char IndentCharacter = '\t';

    /// <summary>
    /// Reads the value at a key path, or <see langword="null"/> when the path does not exist.
    /// </summary>
    public static string? GetValue(string document, IReadOnlyList<string> keyPath)
    {
        var scan = Scan(document, keyPath);

        return scan.ValueStart >= 0
            ? Unescape(document.Substring(scan.ValueStart, scan.ValueLength))
            : null;
    }

    /// <summary>
    /// Returns the document with the value at a key path set, creating the key — and any missing
    /// objects above it — where necessary. A key at index <c>i</c> of the path is written
    /// <c>i</c> tabs in, matching the blocks Steam writes itself: the file parses either way, but
    /// a stray indent reads as corruption to anyone diffing it.
    /// </summary>
    /// <returns>
    /// The updated document, or <see langword="null"/> when not even the root object could be
    /// found, which means the file is not the document we were expecting.
    /// </returns>
    public static string? SetValue(string document, IReadOnlyList<string> keyPath, string value)
    {
        var scan = Scan(document, keyPath);
        var escaped = Escape(value);

        if (scan.ValueStart >= 0)
        {
            return string.Concat(
                document.AsSpan(0, scan.ValueStart),
                escaped,
                document.AsSpan(scan.ValueStart + scan.ValueLength));
        }

        if (scan.DeepestExistingDepth < 0)
        {
            return null;
        }

        var depth = scan.DeepestExistingDepth;
        var insertAt = scan.InsertAt;
        var builder = new StringBuilder();

        for (var level = depth; level < keyPath.Count - 1; level++)
        {
            var indent = new string(IndentCharacter, level);

            builder.Append(indent).Append('"').Append(Escape(keyPath[level])).Append("\"\n");
            builder.Append(indent).Append("{\n");
        }

        builder
            .Append(new string(IndentCharacter, keyPath.Count - 1))
            .Append('"').Append(Escape(keyPath[^1])).Append("\"\t\t\"").Append(escaped).Append("\"\n");

        for (var level = keyPath.Count - 2; level >= depth; level--)
        {
            builder.Append(new string(IndentCharacter, level)).Append("}\n");
        }

        return string.Concat(document.AsSpan(0, insertAt), builder.ToString(), document.AsSpan(insertAt));
    }

    /// <summary>
    /// Walks the document once, recording where the target value is and — failing that — where a
    /// new one would have to go.
    /// </summary>
    private static ScanResult Scan(string document, IReadOnlyList<string> keyPath)
    {
        var result = new ScanResult();
        var path = new List<string>();
        var position = 0;

        while (position < document.Length)
        {
            SkipInsignificant(document, ref position);

            if (position >= document.Length)
            {
                break;
            }

            if (document[position] == '}')
            {
                if (IsPrefixOf(path, keyPath) && path.Count > result.DeepestExistingDepth)
                {
                    result.DeepestExistingDepth = path.Count;
                    result.InsertAt = StartOfLine(document, position);
                }

                if (path.Count > 0)
                {
                    path.RemoveAt(path.Count - 1);
                }

                position++;

                continue;
            }

            if (document[position] != '"')
            {
                position++;

                continue;
            }

            var key = ReadQuoted(document, ref position, out _, out _);

            SkipInsignificant(document, ref position);

            if (position < document.Length && document[position] == '{')
            {
                path.Add(key);
                position++;

                continue;
            }

            if (position < document.Length && document[position] == '"')
            {
                var isTarget = path.Count == keyPath.Count - 1 &&
                               IsPrefixOf(path, keyPath) &&
                               string.Equals(key, keyPath[^1], StringComparison.Ordinal);

                ReadQuoted(document, ref position, out var valueStart, out var valueLength);

                if (isTarget)
                {
                    result.ValueStart = valueStart;
                    result.ValueLength = valueLength;

                    return result;
                }
            }
        }

        return result;
    }

    /// <summary>Whether every element of <paramref name="path" /> matches the start of the target.</summary>
    private static bool IsPrefixOf(List<string> path, IReadOnlyList<string> keyPath)
    {
        if (path.Count >= keyPath.Count)
        {
            return false;
        }

        for (var i = 0; i < path.Count; i++)
        {
            if (!string.Equals(path[i], keyPath[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Advances past whitespace and <c>//</c> comments.</summary>
    private static void SkipInsignificant(string document, ref int position)
    {
        while (position < document.Length)
        {
            if (char.IsWhiteSpace(document[position]))
            {
                position++;

                continue;
            }

            if (document[position] == '/' && position + 1 < document.Length && document[position + 1] == '/')
            {
                while (position < document.Length && document[position] != '\n')
                {
                    position++;
                }

                continue;
            }

            return;
        }
    }

    /// <summary>
    /// Reads a quoted string, reporting the span of its raw contents so a caller can splice over
    /// exactly those characters.
    /// </summary>
    private static string ReadQuoted(string document, ref int position, out int start, out int length)
    {
        position++;
        start = position;

        while (position < document.Length && document[position] != '"')
        {
            position += document[position] == '\\' ? 2 : 1;
        }

        position = Math.Min(position, document.Length);
        length = position - start;

        var raw = document.Substring(start, length);

        if (position < document.Length)
        {
            position++;
        }

        return Unescape(raw);
    }

    /// <summary>Finds the start of the line containing a position, so inserts land line-aligned.</summary>
    private static int StartOfLine(string document, int position)
    {
        var start = document.LastIndexOf('\n', Math.Max(0, position - 1));

        return start < 0 ? 0 : start + 1;
    }

    /// <summary>Applies the escapes Steam uses inside quoted values.</summary>
    public static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\n", "\\n")
        .Replace("\t", "\\t");

    /// <summary>Resolves the escapes Steam uses inside quoted values.</summary>
    public static string Unescape(string value)
    {
        if (!value.Contains('\\'))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                builder.Append(value[i]);

                continue;
            }

            builder.Append(value[++i] switch
            {
                'n' => '\n',
                't' => '\t',
                var other => other
            });
        }

        return builder.ToString();
    }

    /// <summary>Where the target value is, or where one would be inserted.</summary>
    private sealed class ScanResult
    {
        public int ValueStart { get; set; } = -1;

        public int ValueLength { get; set; }

        /// <summary>How many levels of the key path already exist as objects.</summary>
        public int DeepestExistingDepth { get; set; } = -1;

        /// <summary>The offset a new key should be inserted at.</summary>
        public int InsertAt { get; set; }
    }
}
