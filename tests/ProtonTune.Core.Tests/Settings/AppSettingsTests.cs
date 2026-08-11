using ProtonTune.Core.Settings;

namespace ProtonTune.Core.Tests.Settings;

/// <summary>
/// The settings file is meant to be readable and therefore editable by hand, so every value has
/// to survive being given something it did not expect.
/// </summary>
public class AppSettingsTests
{
    /// <summary>
    /// What a first run opens on, before anything has been chosen or stored. Rows rather than
    /// cover art: the list fits more games on screen and reads better when scanning.
    /// </summary>
    [Fact]
    public void OpensOnTheListOrderedByName()
    {
        var settings = new AppSettings();

        Assert.Equal(LibraryViewMode.List, settings.LibraryView);
        Assert.Equal(LibrarySortOrder.Name, settings.LibrarySort);
    }

    /// <summary>
    /// A file written before these existed has no value for them, which deserializes to zero —
    /// so the defaults have to be what zero means, not something a constructor supplies.
    /// </summary>
    [Fact]
    public void ReadsAnAbsentPreferenceAsTheDefault()
    {
        Assert.Equal(LibraryViewMode.List, default);
        Assert.Equal(LibrarySortOrder.Name, default);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    public void FallsBackToTheDefaultViewWhenAskedForOneThatDoesNotExist(int stored) =>
        Assert.Equal(
            LibraryViewMode.List,
            (new AppSettings { LibraryView = (LibraryViewMode)stored }).Sanitised().LibraryView);

    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    public void FallsBackToTheDefaultOrderWhenAskedForOneThatDoesNotExist(int stored) =>
        Assert.Equal(
            LibrarySortOrder.Name,
            (new AppSettings { LibrarySort = (LibrarySortOrder)stored }).Sanitised().LibrarySort);

    [Fact]
    public void KeepsAViewThatDoesExist() =>
        Assert.Equal(
            LibraryViewMode.Grid,
            (new AppSettings { LibraryView = LibraryViewMode.Grid }).Sanitised().LibraryView);

    [Fact]
    public void KeepsAnOrderThatDoesExist() =>
        Assert.Equal(
            LibrarySortOrder.RecentlyPlayed,
            (new AppSettings { LibrarySort = LibrarySortOrder.RecentlyPlayed }).Sanitised().LibrarySort);

    [Theory]
    [InlineData(0, AppSettings.MinimumBackupsToKeep)]
    [InlineData(-5, AppSettings.MinimumBackupsToKeep)]
    [InlineData(5000, AppSettings.MaximumBackupsToKeep)]
    [InlineData(25, 25)]
    public void HoldsTheRetentionInsideWhatIsAllowed(int stored, int expected) =>
        Assert.Equal(expected, (new AppSettings { BackupsToKeep = stored }).Sanitised().BackupsToKeep);

    /// <summary>Sanitising one value must not quietly reset the others.</summary>
    [Fact]
    public void LeavesEverythingElseAloneWhileCorrectingOneValue()
    {
        var settings = new AppSettings
        {
            BackupsToKeep = 5000,
            LibraryView = LibraryViewMode.Grid,
            LibrarySort = LibrarySortOrder.RecentlyPlayed
        }.Sanitised();

        Assert.Equal(AppSettings.MaximumBackupsToKeep, settings.BackupsToKeep);
        Assert.Equal(LibraryViewMode.Grid, settings.LibraryView);
        Assert.Equal(LibrarySortOrder.RecentlyPlayed, settings.LibrarySort);
    }
}
