namespace ProtonTune.Services.Storage;

/// <summary>
/// Where ProtonTune keeps the things it manages on the user's behalf.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately outside the application directory, so that what the user has configured survives
/// ProtonTune being updated, moved, or reinstalled.
/// </para>
/// <para>
/// The root is a property rather than a constant so tests can be pointed at a temporary
/// directory. Without that they write real files into the user's home, over the profile the
/// application later reads.
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

    /// <summary>The global profile and the games following it.</summary>
    public string ProfileFile => Path.Combine(Root, "profile.json");

    /// <summary>ProtonTune's own settings, as opposed to anything it does to a game.</summary>
    public string SettingsFile => Path.Combine(Root, "settings.json");
}
