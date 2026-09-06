using ProtonTune.Core.Launch;

namespace ProtonTune.Core.Tests.Launch;

/// <summary>
/// How a section's settings are broken into the headings its file declares.
/// </summary>
/// <remarks>
/// A run over the declared order rather than a lookup by name: the file's order is what a reader
/// sees, so reordering or merging would move settings behind the writer's back.
/// </remarks>
public class SettingGroupTests
{
    private static readonly SettingCategory Nvidia = new("nvidia", "Nvidia", 1);

    private static SettingDefinition Definition(string variable, string? group = null) =>
        new(variable, Nvidia, variable) { Group = group };

    [Fact]
    public void GroupsNothingIntoNothing() => Assert.Empty(SettingCatalog.Group([]));

    /// <summary>A section that declares no headings is the flat list it always was.</summary>
    [Fact]
    public void LeavesAnUngroupedSectionAsOneUnnamedRun()
    {
        var groups = SettingCatalog.Group([Definition("A"), Definition("B")]);

        var group = Assert.Single(groups);

        Assert.Null(group.Name);
        Assert.Equal(["A", "B"], group.Settings.Select(setting => setting.Variable));
    }

    [Fact]
    public void StartsANewGroupWhereTheHeadingChanges()
    {
        var groups = SettingCatalog.Group([
            Definition("A", "DLSS"),
            Definition("B", "DLSS"),
            Definition("C", "NVAPI")
        ]);

        Assert.Equal(["DLSS", "NVAPI"], groups.Select(group => group.Name));
        Assert.Equal(["A", "B"], groups[0].Settings.Select(setting => setting.Variable));
        Assert.Equal(["C"], groups[1].Settings.Select(setting => setting.Variable));
    }

    /// <summary>
    /// The ungrouped settings a file declares before its first heading keep their place at the
    /// top rather than being swept into the group that follows them.
    /// </summary>
    [Fact]
    public void KeepsTheUngroupedRunAheadOfTheFirstHeading()
    {
        var groups = SettingCatalog.Group([Definition("A"), Definition("B", "DLSS")]);

        Assert.Equal([null, "DLSS"], groups.Select(group => group.Name));
    }

    /// <summary>
    /// A run rather than a lookup, so a heading written twice stays two runs. Merging them would
    /// lift a setting out of the order its file put it in.
    /// </summary>
    [Fact]
    public void KeepsAHeadingUsedTwiceAsTwoRuns()
    {
        var groups = SettingCatalog.Group([
            Definition("A", "DLSS"),
            Definition("B", "NVAPI"),
            Definition("C", "DLSS")
        ]);

        Assert.Equal(["DLSS", "NVAPI", "DLSS"], groups.Select(group => group.Name));
    }

    /// <summary>Nothing may be lost on the way through, whatever the headings say.</summary>
    [Fact]
    public void KeepsEverySettingInItsOriginalOrder()
    {
        SettingDefinition[] definitions =
        [
            Definition("A"),
            Definition("B", "DLSS"),
            Definition("C", "DLSS"),
            Definition("D", "NVAPI")
        ];

        Assert.Equal(
            definitions,
            SettingCatalog.Group(definitions).SelectMany(group => group.Settings));
    }

    /// <summary>
    /// The catalogue groups one section at a time, so a heading of the same name in another
    /// section is a separate run and never joins this one.
    /// </summary>
    [Fact]
    public void GroupsOneSectionAtATime()
    {
        var graphics = new SettingCategory("graphics", "Graphics", 2);

        var catalog = new SettingCatalog(
            [Nvidia, graphics],
            [
                new SettingDefinition("A", Nvidia, "A") { Group = "Shared" },
                new SettingDefinition("B", graphics, "B") { Group = "Shared" }
            ]);

        var group = Assert.Single(catalog.GroupsIn(Nvidia));

        Assert.Equal("Shared", group.Name);
        Assert.Equal(["A"], group.Settings.Select(setting => setting.Variable));
    }
}
