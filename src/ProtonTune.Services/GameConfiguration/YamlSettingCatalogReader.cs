using Microsoft.Extensions.Logging;
using ProtonTune.Core.Launch;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ProtonTune.Services.GameConfiguration;

/// <summary>
/// Builds the setting catalogue by reading the definition files shipped beside the application.
/// </summary>
/// <remarks>
/// One unreadable file costs its own section and nothing else. These are edited by hand, so a
/// mistake in one is expected — refusing to start, or dropping every setting because a single
/// file has a stray character, would turn a typo into an unusable application.
/// </remarks>
public sealed class YamlSettingCatalogReader(string directory, ILogger<YamlSettingCatalogReader> logger)
{
    /// <summary>Where the definition files sit once the application is built.</summary>
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "settings");

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        // The files are written in camelCase, matching how the properties read in prose.
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public SettingCatalog Read()
    {
        string[] paths;

        try
        {
            if (!Directory.Exists(directory))
            {
                logger.LogWarning("No setting definitions were found at {Directory}.", directory);

                return SettingCatalog.Empty;
            }

            paths = Directory.GetFiles(directory, "*.yaml", SearchOption.TopDirectoryOnly);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogError(e, "Could not list setting definitions in {Directory}.", directory);

            return SettingCatalog.Empty;
        }

        var categories = new List<SettingCategory>();
        var definitions = new List<SettingDefinition>();

        // Ordered by path so that two files claiming the same order still load the same way twice.
        foreach (var path in paths.Order(StringComparer.Ordinal))
        {
            if (ReadFile(path) is not { } file || file.Id is not { Length: > 0 } id)
            {
                continue;
            }

            var category = new SettingCategory(id, file.Title ?? id, file.Order);

            categories.Add(category);
            definitions.AddRange(file.Settings.Select(entry => Convert(entry, category, path)).OfType<SettingDefinition>());
        }

        logger.LogInformation(
            "Read {SettingCount} settings in {CategoryCount} sections from {Directory}.",
            definitions.Count,
            categories.Count,
            directory);

        return new SettingCatalog(categories, definitions);
    }

    private SettingDefinitionFile? ReadFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            var file = Deserializer.Deserialize<SettingDefinitionFile>(text);

            if (file is null || string.IsNullOrWhiteSpace(file.Id))
            {
                logger.LogWarning("Skipped {Path}; it names no section id.", path);

                return null;
            }

            return file;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or YamlException)
        {
            logger.LogError(e, "Skipped {Path}; it could not be read.", path);

            return null;
        }
    }

    private SettingDefinition? Convert(SettingDefinitionFile.SettingEntry entry, SettingCategory category, string path)
    {
        if (entry.Variable is not { Length: > 0 } variable)
        {
            logger.LogWarning("Skipped a setting in {Path}; it names no variable.", path);

            return null;
        }

        return new SettingDefinition(variable, category, entry.Label ?? variable)
        {
            Description = entry.Description,
            Kind = ParseKind(entry.Kind, variable, path),
            OnValue = entry.On is { Length: > 0 } on ? on : "1",
            Choices = entry.Choices,
            Placeholder = entry.Placeholder,
            ProtonBuilds = entry.ProtonBuilds,
            RestrictToProtonBuild = entry.RestrictToProtonBuild,
            Compound = Convert(entry.Compound, variable, path)
        };
    }

    /// <summary>
    /// Reads the shape of a variable that packs several settings into one string.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> where none is declared, and also where one is declared with no
    /// options at all — an empty compound would replace the text box with an editor offering
    /// nothing, which is worse than the text box.
    /// </returns>
    private CompoundSchema? Convert(SettingDefinitionFile.CompoundBlock? block, string variable, string path)
    {
        if (block is null)
        {
            return null;
        }

        var groups = block.Groups
            .Select(group => new CompoundOptionGroup(
                group.Name,
                group.Options
                    .Select(option => Convert(option, variable, path))
                    .OfType<CompoundOptionDefinition>()
                    .ToList()))
            .Where(group => group.Options.Count > 0)
            .ToList();

        if (groups.Count == 0)
        {
            logger.LogWarning("{Variable} in {Path} declares a compound with no options.", variable, path);

            return null;
        }

        return new CompoundSchema(
            block.Separator is { Length: > 0 } separator ? separator : CompoundSchema.DefaultSeparator,
            block.Assignment is { Length: > 0 } assignment ? assignment : CompoundSchema.DefaultAssignment,
            groups);
    }

    private CompoundOptionDefinition? Convert(SettingDefinitionFile.OptionEntry option, string variable, string path)
    {
        if (option.Key is not { Length: > 0 } key)
        {
            logger.LogWarning("Skipped an option of {Variable} in {Path}; it names no key.", variable, path);

            return null;
        }

        return new CompoundOptionDefinition(key, option.Label ?? key)
        {
            // Unlike a setting, an option with no kind is a flag: these formats write the bare key.
            Kind = option.Kind is { Length: > 0 }
                ? ParseKind(option.Kind, $"{variable}.{key}", path)
                : SettingKind.Toggle,
            Choices = option.Choices,
            Placeholder = option.Placeholder,
            Description = option.Description
        };
    }

    /// <summary>
    /// Reads the kind, falling back to a text box. An unrecognised kind still gives an editable
    /// setting, which is the least surprising way to be wrong.
    /// </summary>
    private SettingKind ParseKind(string? kind, string variable, string path)
    {
        if (kind is null or { Length: 0 })
        {
            return SettingKind.Text;
        }

        // Names only. Enum.TryParse also accepts the underlying numbers, so an unquoted 2 in a
        // file would quietly become a text box rather than being reported as the mistake it is.
        if (!char.IsAsciiDigit(kind[0]) && Enum.TryParse<SettingKind>(kind, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        logger.LogWarning(
            "{Variable} in {Path} asks for an unknown kind '{Kind}', so it is edited as text.",
            variable,
            path,
            kind);

        return SettingKind.Text;
    }
}
