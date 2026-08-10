namespace ProtonTune.Core.Launch;

/// <summary>
/// One entry in a MangoHud configuration: either a bare flag such as <c>fps</c>, or a setting
/// with a value such as <c>fps_limit=224</c>.
/// </summary>
/// <param name="Key">The option name.</param>
/// <param name="Value">The value, or <see langword="null"/> when the option is a bare flag.</param>
public sealed record MangoHudOption(string Key, string? Value)
{
    /// <summary>Renders the entry as it appears in the comma-separated list.</summary>
    public override string ToString() => Value is null ? Key : $"{Key}={Value}";
}

/// <summary>
/// The contents of <c>MANGOHUD_CONFIG</c>: a comma-separated list of flags and settings.
/// </summary>
/// <remarks>
/// Entries keep their original order, and anything ProtonTune has no definition for is carried
/// through untouched — the same rule the launch options parser follows, for the same reason.
/// </remarks>
public sealed record MangoHudConfig
{
    /// <summary>The entries, in the order they were written.</summary>
    public IReadOnlyList<MangoHudOption> Options { get; init; } = [];

    /// <summary>Whether the configuration is empty.</summary>
    public bool IsEmpty => Options.Count == 0;

    /// <summary>Reads a <c>MANGOHUD_CONFIG</c> value.</summary>
    public static MangoHudConfig Parse(string? config)
    {
        var options = new List<MangoHudOption>();

        foreach (var entry in (config ?? string.Empty).Split(',', StringSplitOptions.TrimEntries))
        {
            if (entry.Length == 0)
            {
                continue;
            }

            var separator = entry.IndexOf('=');

            if (separator < 0)
            {
                // A bare number continues the previous setting rather than standing alone —
                // MangoHud takes lists like fps_limit=0,30,60, and it has no numeric flags. Left
                // as its own entry it would read as an unknown option and display wrongly.
                if (options.Count > 0 && entry.All(char.IsAsciiDigit) && options[^1].Value is { } previous)
                {
                    options[^1] = options[^1] with { Value = $"{previous},{entry}" };

                    continue;
                }

                options.Add(new MangoHudOption(entry, null));

                continue;
            }

            options.Add(new MangoHudOption(entry[..separator], entry[(separator + 1)..]));
        }

        return new MangoHudConfig { Options = options };
    }

    /// <summary>Renders back to a <c>MANGOHUD_CONFIG</c> value.</summary>
    public string Format() => string.Join(',', Options);

    /// <summary>Whether an option is present, with or without a value.</summary>
    public bool Contains(string key) =>
        Options.Any(option => string.Equals(option.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// The value of an option, or <see langword="null"/> when it is absent or a bare flag. Use
    /// <see cref="Contains" /> to tell those apart.
    /// </summary>
    public string? GetValue(string key) =>
        Options.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.Ordinal))?.Value;

    /// <summary>
    /// Returns a copy with an option set, replacing it where it stands or appending it when new.
    /// A <see langword="null"/> value writes a bare flag.
    /// </summary>
    public MangoHudConfig Set(string key, string? value)
    {
        var options = Options.ToList();
        var index = options.FindIndex(option => string.Equals(option.Key, key, StringComparison.Ordinal));

        if (index >= 0)
        {
            options[index] = new MangoHudOption(key, value);
        }
        else
        {
            options.Add(new MangoHudOption(key, value));
        }

        return this with { Options = options };
    }

    /// <summary>Returns a copy without an option.</summary>
    public MangoHudConfig Remove(string key) =>
        this with
        {
            Options = Options
                .Where(option => !string.Equals(option.Key, key, StringComparison.Ordinal))
                .ToList()
        };

    /// <summary>
    /// Replaces every entry ProtonTune has no definition for with the given list, leaving the
    /// recognised ones where they are. This is what backs the free-text field for anything the
    /// editor does not cover.
    /// </summary>
    public MangoHudConfig ReplaceUnrecognised(IEnumerable<MangoHudOption> replacements)
    {
        var recognised = Options.Where(option => MangoHudCatalog.Find(option.Key) is not null);

        return this with { Options = [.. recognised, .. replacements] };
    }

    /// <summary>The entries with no definition, in order.</summary>
    public IReadOnlyList<MangoHudOption> Unrecognised =>
        Options.Where(option => MangoHudCatalog.Find(option.Key) is null).ToList();
}
