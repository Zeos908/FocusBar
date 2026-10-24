using FocusBar.Core.Pins;

namespace FocusBar.Core.Tests;

public class TaskbarButtonsTests
{
    private sealed record Win(int Handle, string? App);

    private const string Chrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    private const string Word = @"C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE";
    private const string Notepad = @"C:\Windows\System32\notepad.exe";

    private static IReadOnlyList<TaskbarButton<Win>> Build(IReadOnlyList<PinnedApp> pins, params Win[] windows) =>
        TaskbarButtons.Build(pins, windows, w => w.App, w => w.Handle.ToString());

    [Fact]
    public void Windows_of_the_same_app_share_one_button()
    {
        var buttons = Build([], new Win(1, Chrome), new Win(2, Notepad), new Win(3, Chrome), new Win(4, Chrome));

        Assert.Equal(2, buttons.Count);
        Assert.Equal([1, 3, 4], buttons[0].Windows.Select(w => w.Handle));
        Assert.Equal([2], buttons[1].Windows.Select(w => w.Handle));
    }

    [Fact]
    public void Paths_are_matched_ignoring_case()
    {
        var buttons = Build([], new Win(1, Word), new Win(2, Word.ToLowerInvariant()));
        Assert.Single(buttons);
    }

    [Fact]
    public void Apps_keep_the_order_they_were_opened_in()
    {
        var buttons = Build([], new Win(1, Notepad), new Win(2, Chrome), new Win(3, Notepad));
        Assert.Equal([Notepad.ToLowerInvariant(), Chrome.ToLowerInvariant()], buttons.Select(b => b.Key["app:".Length..]));
    }

    [Fact]
    public void Pinned_apps_come_first_and_collect_their_windows()
    {
        PinnedApp[] pins = [new(Word, "Word"), new(Chrome, "Chrome")];

        var buttons = Build(pins, new Win(1, Notepad), new Win(2, Chrome), new Win(3, Chrome));

        Assert.Equal(3, buttons.Count);
        Assert.Equal("Word", buttons[0].Pin!.Name);
        Assert.Empty(buttons[0].Windows); // pinned, not open
        Assert.Equal("Chrome", buttons[1].Pin!.Name);
        Assert.Equal([2, 3], buttons[1].Windows.Select(w => w.Handle));
        Assert.Null(buttons[2].Pin);
        Assert.Equal([1], buttons[2].Windows.Select(w => w.Handle));
    }

    [Fact]
    public void A_pinned_shortcut_collects_the_windows_of_the_program_it_opens()
    {
        PinnedApp[] pins = [new(@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Google Chrome.lnk", "Google Chrome", Chrome)];

        var buttons = Build(pins, new Win(1, Chrome));

        Assert.Equal([1], Assert.Single(buttons).Windows.Select(w => w.Handle));
    }

    [Fact]
    public void Store_apps_match_their_pin_by_app_id()
    {
        const string calculator = PinnedApp.AppxPrefix + "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
        var buttons = Build([new PinnedApp(calculator, "Calculator")], new Win(1, calculator), new Win(2, calculator));
        Assert.Equal(2, Assert.Single(buttons).Windows.Count);
    }

    [Fact]
    public void Windows_without_a_program_each_get_a_button()
    {
        var buttons = Build([], new Win(1, null), new Win(2, ""), new Win(3, null));

        Assert.Equal(3, buttons.Count);
        Assert.Equal(["window:1", "window:2", "window:3"], buttons.Select(b => b.Key));
    }

    [Fact]
    public void Keys_stay_the_same_as_windows_come_and_go()
    {
        PinnedApp[] pins = [new(Chrome, "Chrome")];
        var before = Build(pins, new Win(1, Notepad));
        var after = Build(pins, new Win(1, Notepad), new Win(2, Chrome), new Win(5, Notepad));

        Assert.Equal(before.Select(b => b.Key), after.Select(b => b.Key));
    }
}
