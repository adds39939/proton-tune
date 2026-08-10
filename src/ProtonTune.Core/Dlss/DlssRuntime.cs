namespace ProtonTune.Core.Dlss;

/// <summary>
/// A set of DLSS libraries ProtonTune ships, identified by the version they came from.
/// </summary>
/// <param name="Version">The version, taken from the directory the files were found in.</param>
/// <param name="Files">The library files, keyed by file name.</param>
public sealed record DlssRuntime(string Version, IReadOnlyDictionary<string, string> Files)
{
    /// <summary>The library file names this runtime can replace.</summary>
    public IEnumerable<string> FileNames => Files.Keys;

    /// <summary>Whether this runtime provides a replacement for a given library.</summary>
    public bool Provides(string fileName) => Files.ContainsKey(fileName);
}
