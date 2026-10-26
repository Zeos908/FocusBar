using FocusBar.Core.Pins;

namespace FocusBar.Core.Tests;

public class WindowsTaskbarPinsTests
{
    private const string Folder = @"C:\Users\sam\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar";
    private static readonly string Copies = Path.Combine("data", "pins");
    private static readonly DateTime Day = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    private static WindowsPin Shortcut(string name, string? target, int minutes) => new($@"{Folder}\{name}", target, Day.AddMinutes(minutes));

    [Fact]
    public void Pins_in_the_order_they_were_pinned_pointing_at_FocusBars_copies()
    {
        var plan = WindowsTaskbarPins.Plan(
        [
            Shortcut("Google Chrome.lnk", @"C:\Program Files\Google\Chrome\Application\chrome.exe", 30),
            Shortcut("File Explorer.lnk", @"C:\Windows\explorer.exe", 0),
            Shortcut("Microsoft Edge.lnk", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", 10),
        ], Copies);

        Assert.Equal(["File Explorer", "Microsoft Edge", "Google Chrome"], plan.Select(p => p.Pin.Name));
        Assert.Equal(Path.Combine(Copies, "File Explorer.lnk"), plan[0].Pin.Path);
        Assert.Equal(@"C:\Windows\explorer.exe", plan[0].Pin.TargetPath);
        Assert.Equal($@"{Folder}\File Explorer.lnk", plan[0].Source.ShortcutPath);
    }

    [Fact]
    public void Same_time_falls_back_to_name_order()
    {
        var plan = WindowsTaskbarPins.Plan([Shortcut("b.lnk", null, 0), Shortcut("A.lnk", null, 0)], Copies);
        Assert.Equal(["A", "b"], plan.Select(p => p.Pin.Name));
    }

    [Fact]
    public void Skips_other_files_FocusBar_itself_and_duplicates()
    {
        var plan = WindowsTaskbarPins.Plan(
        [
            Shortcut("desktop.ini", null, 0),
            Shortcut("FocusBar.lnk", @"C:\Program Files\FocusBar\FocusBar.exe", 1),
            Shortcut("Chrome.lnk", @"C:\Apps\chrome.exe", 2),
            Shortcut("Chrome (2).lnk", @"C:\APPS\CHROME.EXE", 3),
        ], Copies);

        Assert.Equal("Chrome", Assert.Single(plan).Pin.Name);
    }

    [Fact]
    public void Shortcuts_Windows_cant_resolve_are_still_pinned()
    {
        var plan = WindowsTaskbarPins.Plan([Shortcut("Some Store App.lnk", "", 0)], Copies);
        Assert.Null(Assert.Single(plan).Pin.TargetPath);
    }

    [Fact]
    public void Nothing_pinned_means_nothing_to_do() => Assert.Empty(WindowsTaskbarPins.Plan([], Copies));
}
