using Microsoft.Extensions.Logging;
using ProtonTune.Core.Dlss;

namespace ProtonTune.Services.Dlss;

/// <inheritdoc cref="IDlssRuntimeProvider" />
/// <remarks>
/// Versions are directories under <c>lib/dlss</c> beside the application, each holding the
/// <c>nvngx_*.dll</c> files of that release. Keeping them in versioned directories rather than
/// one flat folder means several can be shipped at once and a game can stay on an older one.
/// </remarks>
public sealed class ShippedDlssRuntimeProvider(ILogger<ShippedDlssRuntimeProvider> logger) : IDlssRuntimeProvider
{
    /// <summary>The libraries ProtonTune is willing to replace.</summary>
    private static readonly string[] KnownLibraries = ["nvngx_dlss.dll", "nvngx_dlssg.dll"];

    private IReadOnlyList<DlssRuntime>? _runtimes;

    /// <inheritdoc />
    public IReadOnlyList<DlssRuntime> GetAll() => _runtimes ??= Read();

    /// <inheritdoc />
    public DlssRuntime? Latest => GetAll().FirstOrDefault();

    private IReadOnlyList<DlssRuntime> Read()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "lib", "dlss");

        if (!Directory.Exists(root))
        {
            logger.LogWarning("No DLSS libraries are shipped at {LibraryRoot}.", root);

            return [];
        }

        try
        {
            var runtimes = Directory
                .EnumerateDirectories(root)
                .Select(ReadVersion)
                .Where(runtime => runtime is not null)
                .Select(runtime => runtime!)
                // Newest first, compared as version numbers so 310.7.0 beats 99.9.9.
                .OrderByDescending(runtime => runtime.Version, VersionComparer.Instance)
                .ToList();

            logger.LogInformation("Found {VersionCount} shipped DLSS versions.", runtimes.Count);

            return runtimes;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not read shipped DLSS libraries from {LibraryRoot}.", root);

            return [];
        }
    }

    /// <summary>Reads one version directory, ignoring anything that holds no known library.</summary>
    private static DlssRuntime? ReadVersion(string directory)
    {
        var files = KnownLibraries
            .Select(name => (Name: name, Path: Path.Combine(directory, name)))
            .Where(file => File.Exists(file.Path))
            .ToDictionary(file => file.Name, file => file.Path, StringComparer.OrdinalIgnoreCase);

        return files.Count > 0
            ? new DlssRuntime(Path.GetFileName(directory), files)
            : null;
    }

    /// <summary>
    /// Orders version directory names numerically where they look like versions, and
    /// alphabetically where they do not.
    /// </summary>
    private sealed class VersionComparer : IComparer<string>
    {
        public static VersionComparer Instance { get; } = new();

        public int Compare(string? x, string? y) =>
            Version.TryParse(x, out var left) && Version.TryParse(y, out var right)
                ? left.CompareTo(right)
                : string.CompareOrdinal(x, y);
    }
}
