namespace ProtonTune.Core.Launch;

/// <summary>
/// One entry of a compound variable: either a bare flag such as <c>fps</c>, or a setting with a
/// value such as <c>fps_limit=224</c>.
/// </summary>
/// <param name="Key">The option name.</param>
/// <param name="Value">The value, or <see langword="null"/> when the option is a bare flag.</param>
public sealed record CompoundEntry(string Key, string? Value)
{
    /// <summary>Renders the entry as it appears inside the variable.</summary>
    public string Render(CompoundSchema schema) =>
        Value is null ? Key : $"{Key}{schema.Assignment}{Value}";
}

/// <summary>
/// The contents of a compound variable, read apart into its entries.
/// </summary>
/// <remarks>
/// Entries keep their original order, and anything ProtonTune has no definition for is carried
/// through untouched — the same rule the launch options parser follows, for the same reason.
/// </remarks>
public sealed record CompoundValue
{
    /// <summary>The shape this was read with, and will be written back in.</summary>
    public required CompoundSchema Schema { get; init; }

    /// <summary>The entries, in the order they were written.</summary>
    public IReadOnlyList<CompoundEntry> Entries { get; init; } = [];

    /// <summary>Whether nothing is configured.</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>Reads a variable's value.</summary>
    public static CompoundValue Parse(CompoundSchema schema, string? value)
    {
        var entries = new List<CompoundEntry>();

        foreach (var entry in (value ?? string.Empty).Split(schema.Separator, StringSplitOptions.TrimEntries))
        {
            if (entry.Length == 0)
            {
                continue;
            }

            var separator = entry.IndexOf(schema.Assignment, StringComparison.Ordinal);

            if (separator < 0)
            {
                // A bare number continues the previous setting rather than standing alone — these
                // formats take lists like fps_limit=0,30,60 inside a comma-separated string, and
                // none of them has a numeric flag. Left as its own entry it would read as an
                // unknown option and display wrongly.
                if (entries.Count > 0 && entry.All(char.IsAsciiDigit) && entries[^1].Value is { } previous)
                {
                    entries[^1] = entries[^1] with { Value = $"{previous}{schema.Separator}{entry}" };

                    continue;
                }

                entries.Add(new CompoundEntry(entry, null));

                continue;
            }

            entries.Add(new CompoundEntry(entry[..separator], entry[(separator + schema.Assignment.Length)..]));
        }

        return new CompoundValue { Schema = schema, Entries = entries };
    }

    /// <summary>Renders back to the variable's value.</summary>
    public string Format() =>
        string.Join(Schema.Separator, Entries.Select(entry => entry.Render(Schema)));

    /// <summary>Whether an option is present, with or without a value.</summary>
    public bool Contains(string key) =>
        Entries.Any(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// The value of an option, or <see langword="null"/> when it is absent or a bare flag. Use
    /// <see cref="Contains" /> to tell those apart.
    /// </summary>
    public string? GetValue(string key) =>
        Entries.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.Ordinal))?.Value;

    /// <summary>
    /// Returns a copy with an option set, replacing it where it stands or appending it when new.
    /// A <see langword="null"/> value writes a bare flag.
    /// </summary>
    public CompoundValue Set(string key, string? value)
    {
        var entries = Entries.ToList();
        var index = entries.FindIndex(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));

        if (index >= 0)
        {
            entries[index] = new CompoundEntry(key, value);
        }
        else
        {
            entries.Add(new CompoundEntry(key, value));
        }

        return this with { Entries = entries };
    }

    /// <summary>Returns a copy without an option.</summary>
    public CompoundValue Remove(string key) =>
        this with
        {
            Entries = Entries
                .Where(entry => !string.Equals(entry.Key, key, StringComparison.Ordinal))
                .ToList()
        };

    /// <summary>The entries with no definition, in order.</summary>
    public IReadOnlyList<CompoundEntry> Unrecognised =>
        Entries.Where(entry => Schema.Find(entry.Key) is null).ToList();

    /// <summary>
    /// Replaces every entry ProtonTune has no definition for with the given list, leaving the
    /// recognised ones where they are. This is what backs the free-text field for anything the
    /// editor does not cover.
    /// </summary>
    public CompoundValue ReplaceUnrecognised(IEnumerable<CompoundEntry> replacements)
    {
        var recognised = Entries.Where(entry => Schema.Find(entry.Key) is not null);

        return this with { Entries = [.. recognised, .. replacements] };
    }
}
