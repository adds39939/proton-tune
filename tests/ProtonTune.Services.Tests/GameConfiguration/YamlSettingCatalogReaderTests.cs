using Microsoft.Extensions.Logging.Abstractions;
using ProtonTune.Core.Launch;
using ProtonTune.Services.GameConfiguration;

namespace ProtonTune.Services.Tests.GameConfiguration;

/// <summary>
/// These files are edited by hand, so the contract is that a mistake in one costs that file and
/// nothing else. Refusing to start, or dropping every setting because one file has a stray
/// character, would turn a typo into an unusable application.
/// </summary>
public sealed class YamlSettingCatalogReaderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("protontune-settings-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private void Write(string name, string contents) =>
        File.WriteAllText(Path.Combine(_directory, name), contents);

    private SettingCatalog Read() =>
        new YamlSettingCatalogReader(_directory, NullLogger<YamlSettingCatalogReader>.Instance).Read();

    [Fact]
    public void ReadsASectionAndItsSettings()
    {
        Write("hdr.yaml", """
            id: hdr
            title: HDR
            order: 2
            settings:
              - variable: DXVK_HDR
                label: Enable HDR in DXVK
                description: Turns on HDR output.
                kind: toggle
            """);

        var catalog = Read();
        var category = Assert.Single(catalog.Categories);
        var definition = Assert.Single(catalog.All);

        Assert.Equal("hdr", category.Id);
        Assert.Equal("HDR", category.Title);
        Assert.Equal(2, category.Order);
        Assert.Equal("DXVK_HDR", definition.Variable);
        Assert.Equal(SettingKind.Toggle, definition.Kind);
        Assert.Equal("Turns on HDR output.", definition.Description);
    }

    /// <summary>Only the variable and label are required; everything else has a sensible default.</summary>
    [Fact]
    public void FallsBackToATextBoxWrittenAsOne()
    {
        Write("graphics.yaml", """
            id: graphics
            settings:
              - variable: DXVK_HUD
                label: DXVK HUD
            """);

        var definition = Assert.Single(Read().All);

        Assert.Equal(SettingKind.Text, definition.Kind);
        Assert.Equal("1", definition.OnValue);
        Assert.Empty(definition.Choices);
        Assert.Empty(definition.ProtonBuilds);
    }

    [Fact]
    public void UsesTheIdAsATitleWhenNoneIsGiven() =>
        Assert.Equal("graphics", ReadOneSection("id: graphics\nsettings: []\n").Title);

    [Theory]
    [InlineData("toggle", SettingKind.Toggle)]
    [InlineData("Choice", SettingKind.Choice)]
    [InlineData("NUMBER", SettingKind.Number)]
    [InlineData("text", SettingKind.Text)]
    public void ReadsEveryKindWhateverTheCasing(string written, SettingKind expected)
    {
        Write("a.yaml", $"""
            id: a
            settings:
              - variable: V
                label: V
                kind: {written}
            """);

        Assert.Equal(expected, Assert.Single(Read().All).Kind);
    }

    /// <summary>
    /// An unknown kind still leaves an editable setting, which is the least surprising way to be
    /// wrong. A number is not a kind either: the enum's underlying values are not the contract.
    /// </summary>
    [Theory]
    [InlineData("slider")]
    [InlineData("2")]
    public void TreatsAnUnknownKindAsText(string written)
    {
        Write("a.yaml", $"""
            id: a
            settings:
              - variable: V
                label: V
                kind: "{written}"
            """);

        Assert.Equal(SettingKind.Text, Assert.Single(Read().All).Kind);
    }

    [Fact]
    public void ReadsTheValueACompoundToggleWrites()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: DXVK_NVAPI_SET_NGX_DEBUG_OPTIONS
                label: Debug info
                kind: toggle
                on: DLSSIndicator=1024
            """);

        Assert.Equal("DLSSIndicator=1024", Assert.Single(Read().All).OnValue);
    }

    [Fact]
    public void ReadsTheBuildsASettingIsDeclaredFor()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: PROTON_DLSS_UPGRADE
                label: Upgrade
                protonBuilds: ["^GE-Proton", "cachyos"]
            """);

        Assert.Equal(["^GE-Proton", "cachyos"], Assert.Single(Read().All).ProtonBuilds);
    }

    /// <summary>
    /// The point of the anchor in the shipped DLSS file: one list written once, used three times.
    /// </summary>
    [Fact]
    public void ResolvesAListWrittenOnceAndReferredTo()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: FIRST
                label: First
                kind: choice
                choices: &presets [one, two]
              - variable: SECOND
                label: Second
                kind: choice
                choices: *presets
            """);

        var catalog = Read();

        Assert.Equal(["one", "two"], catalog.Find("FIRST")!.Choices);
        Assert.Equal(catalog.Find("FIRST")!.Choices, catalog.Find("SECOND")!.Choices);
    }

    [Fact]
    public void ReadsACompoundVariable()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: MANGOHUD_CONFIG
                label: MangoHud options
                compound:
                  separator: ","
                  assignment: "="
                  groups:
                    - name: Frame limiting
                      options:
                        - key: fps_limit
                          label: Frame rate limit
                          kind: text
                          placeholder: "224"
                        - key: fps
                          label: Frame rate
            """);

        var compound = Assert.Single(Read().All).Compound;

        Assert.NotNull(compound);
        Assert.Equal("Frame limiting", Assert.Single(compound.Groups).Name);
        Assert.Equal(SettingKind.Text, compound.Find("fps_limit")!.Kind);
        Assert.Equal("224", compound.Find("fps_limit")!.Placeholder);
    }

    /// <summary>
    /// An option with no kind is a flag, written as the bare key. That differs from a setting,
    /// which defaults to a text box — these formats are mostly flags.
    /// </summary>
    [Fact]
    public void TreatsAnOptionWithNoKindAsAFlag()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: DXVK_HUD
                label: HUD
                compound:
                  groups:
                    - options:
                        - key: fps
                          label: Frame rate
            """);

        Assert.Equal(SettingKind.Toggle, Assert.Single(Read().All).Compound!.Find("fps")!.Kind);
    }

    /// <summary>Most of these formats are comma separated with an equals sign, so neither is required.</summary>
    [Fact]
    public void FallsBackToTheUsualSeparatorAndAssignment()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: DXVK_HUD
                label: HUD
                compound:
                  groups:
                    - options:
                        - key: fps
                          label: Frame rate
            """);

        var compound = Assert.Single(Read().All).Compound!;

        Assert.Equal(",", compound.Separator);
        Assert.Equal("=", compound.Assignment);
    }

    [Fact]
    public void ReadsAFormatThatPacksItselfDifferently()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: WINEDLLOVERRIDES
                label: DLL overrides
                compound:
                  separator: ";"
                  assignment: "="
                  groups:
                    - options:
                        - key: dxgi
                          label: DXGI
                          kind: text
            """);

        var compound = Assert.Single(Read().All).Compound!;

        Assert.Equal(";", compound.Separator);
        Assert.Null(Assert.Single(compound.Groups).Name);
    }

    /// <summary>
    /// An editor offering nothing is worse than the text box it replaced, so a compound with no
    /// usable options is left as an ordinary setting.
    /// </summary>
    [Fact]
    public void IgnoresACompoundWithNoOptions()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: DXVK_HUD
                label: HUD
                compound:
                  groups:
                    - name: Empty
                      options: []
            """);

        Assert.Null(Assert.Single(Read().All).Compound);
    }

    [Fact]
    public void SkipsAnOptionThatNamesNoKey()
    {
        Write("a.yaml", """
            id: a
            settings:
              - variable: DXVK_HUD
                label: HUD
                compound:
                  groups:
                    - options:
                        - label: Nothing to set
                        - key: fps
                          label: Frame rate
            """);

        var compound = Assert.Single(Read().All).Compound!;

        Assert.Equal(["fps"], compound.AllOptions.Select(option => option.Key));
    }

    [Fact]
    public void ReadsACommandAndItsFlags()
    {
        Write("a.yaml", """
            id: gamescope
            command:
              name: gamescope
              label: Launch through Gamescope
              description: Runs the game inside its own compositor.
              terminator: "--"
              groups:
                - name: Output
                  flags:
                    - flag: "-W"
                      aliases: ["--output-width"]
                      label: Output width
                      kind: number
                      placeholder: "3840"
                    - flag: "-f"
                      label: Fullscreen
            settings: []
            """);

        var command = Assert.Single(Read().Categories).Command;

        Assert.NotNull(command);
        Assert.Equal("gamescope", command.Command);
        Assert.Equal("Launch through Gamescope", command.Label);
        Assert.Equal("--", command.Terminator);
        Assert.Equal("Output", Assert.Single(command.Groups).Name);

        var width = command.AllFlags.First();

        Assert.Equal(SettingKind.Number, width.Kind);
        Assert.Equal(["--output-width"], width.Aliases);
        Assert.Equal("3840", width.Placeholder);
    }

    /// <summary>
    /// A flag with no kind is a switch, written bare. That differs from a setting, which defaults
    /// to a text box — command lines are mostly switches.
    /// </summary>
    [Fact]
    public void TreatsAFlagWithNoKindAsASwitch()
    {
        Write("a.yaml", """
            id: a
            command:
              name: gamescope
              groups:
                - flags:
                    - flag: "-f"
                      label: Fullscreen
            settings: []
            """);

        var flag = Assert.Single(Assert.Single(Read().Categories).Command!.AllFlags);

        Assert.Equal(SettingKind.Toggle, flag.Kind);
        Assert.False(flag.TakesValue);
    }

    /// <summary>
    /// Only commands that take arguments of their own declare a terminator. Without one there is
    /// no telling where a command's arguments stop, so it is treated as taking none.
    /// </summary>
    [Fact]
    public void LeavesACommandWithNoTerminatorWithoutOne()
    {
        Write("a.yaml", "id: a\ncommand:\n  name: mangohud\nsettings: []\n");

        Assert.Null(Assert.Single(Read().Categories).Command!.Terminator);
    }

    /// <summary>
    /// Unlike a compound with no options, which loses the editor it was replacing. Here the
    /// command itself is the setting, and launching through it is worth offering alone.
    /// </summary>
    [Fact]
    public void KeepsACommandThatOffersNoFlags()
    {
        Write("a.yaml", "id: a\ncommand:\n  name: gamemoderun\n  label: Launch through GameMode\nsettings: []\n");

        var command = Assert.Single(Read().Categories).Command;

        Assert.NotNull(command);
        Assert.Empty(command.AllFlags);
    }

    [Fact]
    public void SkipsACommandThatNamesNothingToRun()
    {
        Write("a.yaml", "id: a\ncommand:\n  label: Launch through nothing\nsettings: []\n");

        Assert.Null(Assert.Single(Read().Categories).Command);
    }

    [Fact]
    public void SkipsAFlagThatNamesNoFlag()
    {
        Write("a.yaml", """
            id: a
            command:
              name: gamescope
              groups:
                - flags:
                    - label: Nothing to set
                    - flag: "-f"
                      label: Fullscreen
            settings: []
            """);

        Assert.Equal(["-f"], Assert.Single(Read().Categories).Command!.AllFlags.Select(flag => flag.Flag));
    }

    /// <summary>A section is variables, a command, or both; none of the three is required.</summary>
    [Fact]
    public void LeavesASectionOfVariablesWithNoCommand()
    {
        Write("a.yaml", "id: a\nsettings:\n  - variable: DXVK_HDR\n    label: HDR\n");

        Assert.Null(Assert.Single(Read().Categories).Command);
    }

    [Fact]
    public void OneBrokenFileDoesNotCostTheOthers()
    {
        Write("broken.yaml", "id: broken\nsettings:\n  - variable: [unclosed\n");
        Write("good.yaml", """
            id: good
            settings:
              - variable: DXVK_HDR
                label: HDR
            """);

        var catalog = Read();

        Assert.Equal("good", Assert.Single(catalog.Categories).Id);
        Assert.Equal("DXVK_HDR", Assert.Single(catalog.All).Variable);
    }

    [Fact]
    public void SkipsAFileThatNamesNoSection()
    {
        Write("nameless.yaml", "title: Nameless\nsettings:\n  - variable: V\n    label: V\n");

        Assert.Empty(Read().Categories);
    }

    /// <summary>A setting with no variable is not a setting; the rest of its file still loads.</summary>
    [Fact]
    public void SkipsASettingThatNamesNoVariable()
    {
        Write("a.yaml", """
            id: a
            settings:
              - label: Nothing to set
              - variable: DXVK_HDR
                label: HDR
            """);

        Assert.Equal("DXVK_HDR", Assert.Single(Read().All).Variable);
    }

    [Fact]
    public void ReadsNothingFromADirectoryThatIsNotThere()
    {
        var catalog = new YamlSettingCatalogReader(
            Path.Combine(_directory, "missing"),
            NullLogger<YamlSettingCatalogReader>.Instance).Read();

        Assert.Empty(catalog.Categories);
        Assert.Empty(catalog.All);
    }

    /// <summary>Sections are listed by their declared order, not by the file names.</summary>
    [Fact]
    public void OrdersSectionsAsTheyAsk()
    {
        Write("aaa.yaml", "id: last\norder: 9\nsettings: []\n");
        Write("zzz.yaml", "id: first\norder: 1\nsettings: []\n");

        Assert.Equal(["first", "last"], Read().Categories.Select(category => category.Id));
    }

    private SettingCategory ReadOneSection(string contents)
    {
        Write("only.yaml", contents);

        return Assert.Single(Read().Categories);
    }
}
