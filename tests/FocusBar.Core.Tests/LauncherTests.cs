using FocusBar.Core.Launcher;

namespace FocusBar.Core.Tests;

public sealed class StartMenuScannerTests : IDisposable
{
    private readonly string _user;
    private readonly string _common;
    private readonly string _root;

    public StartMenuScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "FocusBarTests", Guid.NewGuid().ToString("N"));
        _user = Path.Combine(_root, "User", "Programs");
        _common = Path.Combine(_root, "Common", "Programs");
        Directory.CreateDirectory(_user);
        Directory.CreateDirectory(_common);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Touch(string folder, string relative)
    {
        var path = Path.Combine(folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        return path;
    }

    [Fact]
    public void Finds_shortcuts_in_subfolders_sorted_by_name()
    {
        Touch(_common, "Zoom.lnk");
        Touch(_common, Path.Combine("Accessories", "Notepad.lnk"));
        Touch(_user, Path.Combine("Games", "Minecraft Launcher.lnk"));

        var apps = StartMenuScanner.Scan([_user, _common]);

        Assert.Equal(["Minecraft Launcher", "Notepad", "Zoom"], apps.Select(a => a.Name));
    }

    [Fact]
    public void Includes_web_and_ClickOnce_shortcuts_but_not_other_files()
    {
        Touch(_common, "Docs.url");
        Touch(_common, "Tool.appref-ms");
        Touch(_common, "readme.txt");
        Touch(_common, "desktop.ini");

        var apps = StartMenuScanner.Scan([_common]);

        Assert.Equal(["Docs", "Tool"], apps.Select(a => a.Name));
    }

    [Fact]
    public void Skips_uninstallers()
    {
        Touch(_common, Path.Combine("Audacity", "Audacity.lnk"));
        Touch(_common, Path.Combine("Audacity", "Uninstall Audacity.lnk"));

        Assert.Equal(["Audacity"], StartMenuScanner.Scan([_common]).Select(a => a.Name));
    }

    [Fact]
    public void Current_users_shortcut_wins_over_a_duplicate_name()
    {
        var mine = Touch(_user, "Discord.lnk");
        Touch(_common, "discord.lnk");

        var apps = StartMenuScanner.Scan([_user, _common]);

        Assert.Equal(mine, Assert.Single(apps).Path);
    }

    [Fact]
    public void Missing_folders_are_skipped()
    {
        Touch(_common, "Notepad.lnk");
        var apps = StartMenuScanner.Scan([Path.Combine(_root, "nope"), "", _common]);
        Assert.Single(apps);
    }
}

public class AppSearchTests
{
    private static readonly AppEntry[] Apps =
    [
        new("Google Chrome", "c"),
        new("Visual Studio Code", "vsc"),
        new("Microsoft Word", "w"),
        new("Word Hunt", "wh"),
        new("Notepad", "n"),
        new("Notepad++", "npp"),
        new("Spotify", "s"),
        new("Microsoft Teams", "t"),
    ];

    private static string[] Names(string query) => [.. AppSearch.Filter(Apps, query).Select(a => a.Name)];

    [Fact]
    public void Empty_query_lists_everything_alphabetically()
    {
        Assert.Equal(Apps.Select(a => a.Name).Order(StringComparer.CurrentCultureIgnoreCase), Names("  "));
        Assert.Equal(Apps.Length, AppSearch.Filter(Apps, null).Count);
    }

    [Fact]
    public void Exact_match_comes_first_then_prefix()
    {
        Assert.Equal(["Notepad", "Notepad++"], Names("notepad"));
    }

    [Fact]
    public void Name_prefix_beats_word_prefix()
    {
        Assert.Equal(["Word Hunt", "Microsoft Word"], Names("word"));
    }

    [Fact]
    public void Matches_the_start_of_any_word()
    {
        Assert.Equal(["Google Chrome"], Names("chr"));
    }

    [Fact]
    public void Matches_initials()
    {
        Assert.Equal(["Visual Studio Code"], Names("vsc"));
        Assert.Equal(["Microsoft Teams", "Microsoft Word"], Names("mi"));
    }

    [Fact]
    public void Falls_back_to_text_anywhere_in_the_name()
    {
        Assert.Equal(["Spotify"], Names("tif"));
    }

    [Fact]
    public void No_match_returns_nothing()
    {
        Assert.Empty(Names("fortnite"));
    }

    [Fact]
    public void Is_case_insensitive_and_trims()
    {
        Assert.Equal(["Spotify"], Names("  SPOT "));
    }
}
