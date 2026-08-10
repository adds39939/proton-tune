using ProtonTune.Services.Steam;

namespace ProtonTune.Services.Tests.Steam;

/// <summary>
/// These edits land in localconfig.vdf, which holds a user's entire Steam client configuration.
/// The contract is narrow on purpose: change the one value asked for, and copy every other byte
/// through unchanged.
/// </summary>
public class SteamConfigTextTests
{
    /// <summary>
    /// Shaped like the real file: tab indented, with a cached JSON blob full of escaped quotes
    /// sitting next to the values being edited. That blob is what makes re-serialising the whole
    /// document too dangerous to consider, so every test carries it along.
    /// </summary>
    private const string Document =
        "\"UserLocalConfigStore\"\n" +
        "{\n" +
        "\t\"Software\"\n" +
        "\t{\n" +
        "\t\t\"Valve\"\n" +
        "\t\t{\n" +
        "\t\t\t\"Steam\"\n" +
        "\t\t\t{\n" +
        "\t\t\t\t\"CachedPrefs\"\t\t\"{\\\"a\\\":1,\\\"b\\\":\\\"two\\\"}\"\n" +
        "\t\t\t\t\"apps\"\n" +
        "\t\t\t\t{\n" +
        "\t\t\t\t\t\"2357570\"\n" +
        "\t\t\t\t\t{\n" +
        "\t\t\t\t\t\t\"LastPlayed\"\t\t\"1786305709\"\n" +
        "\t\t\t\t\t\t\"LaunchOptions\"\t\t\"PROTON_ENABLE_HDR=1 %command%\"\n" +
        "\t\t\t\t\t}\n" +
        "\t\t\t\t\t\"440\"\n" +
        "\t\t\t\t\t{\n" +
        "\t\t\t\t\t\t\"LastPlayed\"\t\t\"1700000000\"\n" +
        "\t\t\t\t\t}\n" +
        "\t\t\t\t}\n" +
        "\t\t\t}\n" +
        "\t\t}\n" +
        "\t}\n" +
        "}\n";

    private static string[] PathTo(string appId) =>
        ["UserLocalConfigStore", "Software", "Valve", "Steam", "apps", appId, "LaunchOptions"];

    [Fact]
    public void ReadsAValueByPath() =>
        Assert.Equal("PROTON_ENABLE_HDR=1 %command%", SteamConfigText.GetValue(Document, PathTo("2357570")));

    [Fact]
    public void ReturnsNullForAKeyThatIsNotSet() =>
        Assert.Null(SteamConfigText.GetValue(Document, PathTo("440")));

    [Fact]
    public void ReturnsNullForAnAppThatIsNotPresent() =>
        Assert.Null(SteamConfigText.GetValue(Document, PathTo("999999")));

    [Fact]
    public void DoesNotConfuseAppsWithTheSameKeyName()
    {
        // Both apps have LastPlayed; the scanner must match the whole path, not just the key.
        var path = new[] { "UserLocalConfigStore", "Software", "Valve", "Steam", "apps", "440", "LastPlayed" };

        Assert.Equal("1700000000", SteamConfigText.GetValue(Document, path));
    }

    [Fact]
    public void ReplacingAValueChangesNothingElse()
    {
        var updated = SteamConfigText.SetValue(Document, PathTo("2357570"), "DXVK_HDR=1 mangohud %command%");

        Assert.NotNull(updated);
        Assert.Equal("DXVK_HDR=1 mangohud %command%", SteamConfigText.GetValue(updated, PathTo("2357570")));

        // Everything outside the edited value is identical, including the escaped JSON blob.
        Assert.Equal(
            Document.Replace("PROTON_ENABLE_HDR=1 %command%", "DXVK_HDR=1 mangohud %command%"),
            updated);
    }

    [Fact]
    public void LeavesTheCachedJsonBlobUntouched()
    {
        var updated = SteamConfigText.SetValue(Document, PathTo("2357570"), "");

        Assert.Contains("\"CachedPrefs\"\t\t\"{\\\"a\\\":1,\\\"b\\\":\\\"two\\\"}\"", updated);
    }

    [Fact]
    public void AddsTheKeyToAnAppThatHasNoLaunchOptions()
    {
        var updated = SteamConfigText.SetValue(Document, PathTo("440"), "PROTON_LOG=1 %command%");

        Assert.NotNull(updated);
        Assert.Equal("PROTON_LOG=1 %command%", SteamConfigText.GetValue(updated, PathTo("440")));

        // The app that already had options is undisturbed.
        Assert.Equal("PROTON_ENABLE_HDR=1 %command%", SteamConfigText.GetValue(updated, PathTo("2357570")));
        Assert.Equal("1700000000", SteamConfigText.GetValue(
            updated, ["UserLocalConfigStore", "Software", "Valve", "Steam", "apps", "440", "LastPlayed"]));
    }

    [Fact]
    public void CreatesTheAppBlockForAGameThatHasNeverBeenLaunched()
    {
        // Configuring a game before its first launch is ordinary, and Steam only writes an app
        // block once it has something to record.
        var updated = SteamConfigText.SetValue(Document, PathTo("993090"), "MANGOHUD_CONFIG=fps_limit=60 %command%");

        Assert.NotNull(updated);
        Assert.Equal("MANGOHUD_CONFIG=fps_limit=60 %command%", SteamConfigText.GetValue(updated, PathTo("993090")));
        Assert.Equal("PROTON_ENABLE_HDR=1 %command%", SteamConfigText.GetValue(updated, PathTo("2357570")));
    }

    /// <summary>
    /// The file parses whatever the indentation, but a block sitting a level deeper than its
    /// siblings reads as corruption to anyone who opens the file or diffs a backup against it.
    /// </summary>
    [Fact]
    public void IndentsAnInsertedBlockLevelWithTheOnesSteamWrote()
    {
        var updated = SteamConfigText.SetValue(Document, PathTo("993090"), "MANGOHUD_CONFIG=fps_limit=60 %command%");

        Assert.NotNull(updated);

        // The same depth the fixture's own app block and its keys are written at.
        Assert.Contains("\n\t\t\t\t\t\"993090\"\n\t\t\t\t\t{\n", updated);
        Assert.Contains("\n\t\t\t\t\t\t\"LaunchOptions\"\t\t\"MANGOHUD_CONFIG=fps_limit=60 %command%\"\n", updated);
        Assert.Contains("%command%\"\n\t\t\t\t\t}\n", updated);
    }

    [Fact]
    public void WritesAValueThatNeedsEscaping()
    {
        var updated = SteamConfigText.SetValue(Document, PathTo("2357570"), "WINEDLLOVERRIDES=\"dxgi=n,b\" %command%");

        Assert.NotNull(updated);
        Assert.Contains("WINEDLLOVERRIDES=\\\"dxgi=n,b\\\"", updated);
        Assert.Equal("WINEDLLOVERRIDES=\"dxgi=n,b\" %command%", SteamConfigText.GetValue(updated, PathTo("2357570")));
    }

    [Fact]
    public void ClearingAValueLeavesAnEmptyString()
    {
        var updated = SteamConfigText.SetValue(Document, PathTo("2357570"), "");

        Assert.NotNull(updated);
        Assert.Equal(string.Empty, SteamConfigText.GetValue(updated, PathTo("2357570")));
    }

    [Fact]
    public void RefusesADocumentThatIsNotWhatWeExpect() =>
        Assert.Null(SteamConfigText.SetValue("\"SomethingElse\"\n{\n}\n", PathTo("2357570"), "x"));

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("has \"quotes\"", "has \\\"quotes\\\"")]
    [InlineData(@"back\slash", @"back\\slash")]
    [InlineData("tab\there", "tab\\there")]
    public void EscapingSurvivesARoundTrip(string value, string escaped)
    {
        Assert.Equal(escaped, SteamConfigText.Escape(value));
        Assert.Equal(value, SteamConfigText.Unescape(escaped));
    }
}
