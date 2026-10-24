using FocusBar.Core.Pins;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Tests;

public sealed class PinnedAppListTests : IDisposable
{
    private const string ChromeLink = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Google Chrome.lnk";
    private const string ChromeExe = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    private const string NotepadExe = @"C:\Windows\System32\notepad.exe";
    private const string Calculator = "appx:Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";

    private readonly TempAppData _temp = new();

    public void Dispose() => _temp.Dispose();

    private static PinnedApp Chrome() => new(ChromeLink, "Google Chrome", ChromeExe);

    [Fact]
    public void Adds_in_order()
    {
        var pins = new PinnedAppList();
        Assert.True(pins.Add(Chrome()));
        Assert.True(pins.Add(new PinnedApp(NotepadExe, "Notepad")));
        Assert.Equal([ChromeLink, NotepadExe], pins.Apps.Select(a => a.Path));
    }

    [Fact]
    public void Refuses_duplicates_ignoring_case()
    {
        var pins = new PinnedAppList();
        pins.Add(new PinnedApp(NotepadExe, "Notepad"));
        Assert.False(pins.Add(new PinnedApp(NotepadExe.ToUpperInvariant(), "NOTEPAD")));
        Assert.Single(pins.Apps);
    }

    [Fact]
    public void Running_exe_is_recognized_as_pinned_through_its_shortcut()
    {
        var pins = new PinnedAppList([Chrome()]);
        Assert.True(pins.Contains(ChromeExe));
        Assert.Equal(ChromeLink, pins.Find(ChromeExe)?.Path);
    }

    [Fact]
    public void Pinning_the_exe_after_its_shortcut_is_a_duplicate()
    {
        var pins = new PinnedAppList([Chrome()]);
        Assert.False(pins.Add(new PinnedApp(ChromeExe, "Google Chrome")));
    }

    [Fact]
    public void Pinning_a_shortcut_after_its_exe_is_a_duplicate()
    {
        var pins = new PinnedAppList([new PinnedApp(ChromeExe, "Google Chrome")]);
        Assert.False(pins.Add(Chrome()));
    }

    [Fact]
    public void Remove_works_by_path_or_shortcut_target()
    {
        var pins = new PinnedAppList([Chrome(), new PinnedApp(Calculator, "Calculator")]);
        Assert.True(pins.Remove(ChromeExe));
        Assert.True(pins.Remove(Calculator));
        Assert.False(pins.Remove(Calculator));
        Assert.Empty(pins.Apps);
    }

    [Fact]
    public void Ignores_blank_paths()
    {
        var pins = new PinnedAppList();
        Assert.False(pins.Add(new PinnedApp("  ", "Nothing")));
        Assert.False(pins.Contains(""));
    }

    [Theory]
    [InlineData(NotepadExe, -1, new[] { NotepadExe, ChromeLink, Calculator })]
    [InlineData(NotepadExe, 1, new[] { ChromeLink, Calculator, NotepadExe })]
    [InlineData(ChromeLink, 5, new[] { NotepadExe, Calculator, ChromeLink })]
    public void Moves_left_and_right(string path, int offset, string[] expected)
    {
        var pins = new PinnedAppList([Chrome(), new PinnedApp(NotepadExe, "Notepad"), new PinnedApp(Calculator, "Calculator")]);
        Assert.True(pins.Move(path, offset));
        Assert.Equal(expected, pins.Apps.Select(a => a.Path));
    }

    [Fact]
    public void Move_past_the_end_reports_no_change()
    {
        var pins = new PinnedAppList([Chrome(), new PinnedApp(NotepadExe, "Notepad")]);
        Assert.False(pins.Move(ChromeLink, -1));
        Assert.False(pins.Move(NotepadExe, 1));
        Assert.False(pins.Move("not pinned", 1));
    }

    [Fact]
    public void Constructor_drops_duplicates_from_a_hand_edited_file()
    {
        var pins = new PinnedAppList([Chrome(), new PinnedApp(ChromeExe, "Chrome again")]);
        Assert.Single(pins.Apps);
    }

    [Fact]
    public void Store_round_trips_pins()
    {
        var store = new PinStore(new JsonFileStore(_temp.Paths));
        var pins = new PinnedAppList([Chrome(), new PinnedApp(Calculator, "Calculator")]);

        store.Save(pins);
        var loaded = store.Load();

        Assert.Equal(pins.Apps, loaded.Apps);
    }

    [Fact]
    public void Store_with_no_file_is_empty()
    {
        var store = new PinStore(new JsonFileStore(_temp.Paths));
        Assert.Empty(store.Load().Apps);
    }
}
