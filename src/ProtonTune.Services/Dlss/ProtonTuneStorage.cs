namespace ProtonTune.Services.Dlss;

/// <summary>
/// Where ProtonTune keeps the things it manages on the user's behalf.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately outside the application directory. Game files are linked to the library store, so
/// those links have to survive ProtonTune being updated, moved, or reinstalled — pointing them at
/// files inside the app would break every game the next time it moved.
/// </para>
/// <para>
/// The root is a property rather than a constant so tests can be pointed at a temporary
/// directory. Without that they write real files into the user's home, and a test fixture named
/// after a real game would sit in the same store the application later reads.
/// </para>
/// </remarks>
public sealed class ProtonTuneStorage
{
    /// <summary>Creates storage under the user's data directory.</summary>
    public ProtonTuneStorage()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "proton-tune"))
    {
    }

    private ProtonTuneStorage(string root) => Root = root;

    /// <summary>Creates storage under a specific directory, for tests.</summary>
    public static ProtonTuneStorage At(string root) => new(root);

    /// <summary>The root of everything ProtonTune stores.</summary>
    public string Root { get; }

    /// <summary>DLSS libraries, by version, that game files are linked to.</summary>
    public string LibraryStore => Path.Combine(Root, "dlss");

    /// <summary>The original files replaced by links, so they can be put back.</summary>
    public string Backups => Path.Combine(Root, "dlss-backup");

    /// <summary>Generated launch scripts.</summary>
    public string Scripts => Path.Combine(Root, "bin");

    /// <summary>The store directory for one runtime version.</summary>
    public string StoreFor(string version) => Path.Combine(LibraryStore, version);

    /// <summary>The backup directory for one game.</summary>
    public string BackupsFor(uint appId) => Path.Combine(Backups, appId.ToString());

    /// <summary>The launch script generated for one game.</summary>
    public string ScriptFor(uint appId) => Path.Combine(Scripts, $"dlss-{appId}.sh");
}
