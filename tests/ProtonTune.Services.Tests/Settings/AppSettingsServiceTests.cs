using Microsoft.Extensions.Logging.Abstractions;
using ProtonTune.Core.Settings;
using ProtonTune.Services.Storage;
using ProtonTune.Services.Settings;

namespace ProtonTune.Services.Tests.Settings;

/// <summary>
/// Storing preferences between sessions. The whole point of remembering how the library was left
/// is that it comes back that way, which only shows up when the file is written and read again.
/// </summary>
public sealed class AppSettingsServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("protontune-settings-").FullName;

    private string SettingsFile => Path.Combine(_root, "settings.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>
    /// A fresh service each time, since one caches what it has read — reusing it would test the
    /// cache rather than the file.
    /// </summary>
    private AppSettingsService CreateService() =>
        new(ProtonTuneStorage.At(_root), NullLogger<AppSettingsService>.Instance);

    [Fact]
    public async Task OpensOnTheDefaultsWhenNothingHasBeenStored()
    {
        var settings = await CreateService().GetAsync();

        Assert.Equal(LibraryViewMode.List, settings.LibraryView);
        Assert.Equal(LibrarySortOrder.Name, settings.LibrarySort);
    }

    [Fact]
    public async Task RemembersHowTheLibraryWasLeft()
    {
        await CreateService().SaveAsync(new AppSettings
        {
            LibraryView = LibraryViewMode.Grid,
            LibrarySort = LibrarySortOrder.RecentlyPlayed
        });

        var reopened = await CreateService().GetAsync();

        Assert.Equal(LibraryViewMode.Grid, reopened.LibraryView);
        Assert.Equal(LibrarySortOrder.RecentlyPlayed, reopened.LibrarySort);
    }

    /// <summary>
    /// Written by name, not by number. A number would tie the file to the order the members are
    /// declared in, and that order is the order the buttons and the menu appear in — so putting
    /// the list first would silently turn everyone's stored grid into a list.
    /// </summary>
    [Fact]
    public async Task WritesThePreferencesByName()
    {
        await CreateService().SaveAsync(new AppSettings
        {
            LibraryView = LibraryViewMode.Grid,
            LibrarySort = LibrarySortOrder.RecentlyPlayed
        });

        var written = await File.ReadAllTextAsync(SettingsFile);

        Assert.Contains("\"Grid\"", written, StringComparison.Ordinal);
        Assert.Contains("\"RecentlyPlayed\"", written, StringComparison.Ordinal);
    }

    /// <summary>
    /// The library writes these while the settings page writes the retention, so a save from one
    /// must carry through what the other put there.
    /// </summary>
    [Fact]
    public async Task KeepsTheRestOfTheSettingsWhenOneChanges()
    {
        await CreateService().SaveAsync(new AppSettings { BackupsToKeep = 25 });

        var service = CreateService();
        var stored = await service.GetAsync();

        await service.SaveAsync(stored with { LibraryView = LibraryViewMode.Grid });

        var reopened = await CreateService().GetAsync();

        Assert.Equal(25, reopened.BackupsToKeep);
        Assert.Equal(LibraryViewMode.Grid, reopened.LibraryView);
    }

    /// <summary>A file written before these preferences existed still has to open.</summary>
    [Fact]
    public async Task ReadsAFileFromBeforeThePreferencesExisted()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(SettingsFile, """{ "BackupsToKeep": 4 }""");

        var settings = await CreateService().GetAsync();

        Assert.Equal(4, settings.BackupsToKeep);
        Assert.Equal(LibraryViewMode.List, settings.LibraryView);
        Assert.Equal(LibrarySortOrder.Name, settings.LibrarySort);
    }

    /// <summary>
    /// These are preferences rather than a record of anything, so an unreadable file costs a
    /// re-choice and never fails the application.
    /// </summary>
    [Fact]
    public async Task FallsBackToTheDefaultsWhenTheFileCannotBeRead()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(SettingsFile, "not json at all");

        var settings = await CreateService().GetAsync();

        Assert.Equal(AppSettings.DefaultBackupsToKeep, settings.BackupsToKeep);
        Assert.Equal(LibraryViewMode.List, settings.LibraryView);
    }
}
