using FocusBar.Core.Focus;
using FocusBar.Core.Models;
using FocusBar.Core.Settings;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Tests;

public class HostsFileTests
{
    private const string Original =
        "# Copyright (c) 1993-2009 Microsoft Corp.\r\n" +
        "#\r\n" +
        "# localhost name resolution is handled within DNS itself.\r\n" +
        "#\t127.0.0.1       localhost\r\n" +
        "192.168.1.20 printer.local\r\n";

    [Fact]
    public void Apply_adds_a_marked_block_and_keeps_everything_else()
    {
        var result = HostsFile.Apply(Original, ["youtube.com"]);

        Assert.StartsWith(Original, result);
        Assert.Contains(HostsFile.BeginMarker + "\r\n", result);
        Assert.Contains("0.0.0.0 youtube.com\r\n", result);
        Assert.Contains("0.0.0.0 www.youtube.com\r\n", result);
        Assert.Contains(":: youtube.com\r\n", result);
        Assert.EndsWith(HostsFile.EndMarker + "\r\n", result);
    }

    [Fact]
    public void Remove_restores_the_original_exactly()
    {
        var blocked = HostsFile.Apply(Original, ["youtube.com", "tiktok.com"]);
        Assert.Equal(Original, HostsFile.Remove(blocked));
    }

    [Fact]
    public void Apply_twice_replaces_rather_than_stacks()
    {
        var once = HostsFile.Apply(Original, ["youtube.com"]);
        var twice = HostsFile.Apply(once, ["reddit.com"]);

        Assert.Single(twice.Split('\n'), l => l.StartsWith("# FocusBar BEGIN"));
        Assert.DoesNotContain("youtube.com", twice);
        Assert.Contains("0.0.0.0 reddit.com", twice);
        Assert.Equal(twice, HostsFile.Apply(twice, ["reddit.com"]));
    }

    [Fact]
    public void Lines_after_the_block_are_kept()
    {
        var withBlock = HostsFile.Apply(Original, ["youtube.com"]) + "10.0.0.5 nas.local\r\n";
        var removed = HostsFile.Remove(withBlock);
        Assert.Equal(Original + "10.0.0.5 nas.local\r\n", removed.Replace("\r\n\r\n10.0.0.5", "\r\n10.0.0.5"));
        Assert.Contains("10.0.0.5 nas.local", removed);
        Assert.DoesNotContain("youtube", removed);
    }

    [Fact]
    public void A_damaged_block_without_an_end_marker_only_loses_its_own_entries()
    {
        var damaged = Original + "\r\n" + HostsFile.BeginMarker + "\r\n0.0.0.0 youtube.com\r\n:: youtube.com\r\n10.0.0.5 nas.local\r\n";
        var removed = HostsFile.Remove(damaged);

        Assert.Contains("10.0.0.5 nas.local", removed);
        Assert.Contains("printer.local", removed);
        Assert.DoesNotContain("youtube", removed);
        Assert.DoesNotContain("FocusBar", removed);
    }

    [Fact]
    public void Applying_no_sites_just_removes_the_block()
    {
        var blocked = HostsFile.Apply(Original, ["youtube.com"]);
        Assert.Equal(Original, HostsFile.Apply(blocked, []));
        Assert.Equal(Original, HostsFile.Apply(blocked, ["not a site"]));
    }

    [Fact]
    public void Works_on_an_empty_file_and_with_unix_line_endings()
    {
        var fromEmpty = HostsFile.Apply("", ["youtube.com"]);
        Assert.StartsWith(HostsFile.BeginMarker, fromEmpty);
        Assert.Equal("", HostsFile.Remove(fromEmpty));

        var unix = "127.0.0.1 localhost\n";
        var blocked = HostsFile.Apply(unix, ["youtube.com"]);
        Assert.DoesNotContain("\r", blocked);
        Assert.Equal(unix, HostsFile.Remove(blocked));
    }

    [Fact]
    public void Reports_what_is_blocked()
    {
        var blocked = HostsFile.Apply(Original, ["youtube.com", "www.reddit.com"]);
        Assert.True(HostsFile.HasBlock(blocked));
        Assert.False(HostsFile.HasBlock(Original));
        Assert.Equal(new HashSet<string> { "youtube.com", "reddit.com" }, HostsFile.BlockedDomains(blocked));
        Assert.Empty(HostsFile.BlockedDomains(Original));
    }

    [Theory]
    [InlineData("youtube.com", "youtube.com")]
    [InlineData("  https://www.YouTube.com/watch?v=abc ", "youtube.com")]
    [InlineData("youtube.com:443", "youtube.com")]
    [InlineData("m.youtube.com", "m.youtube.com")]
    [InlineData("twitch.tv/", "twitch.tv")]
    [InlineData("bücher.de", "xn--bcher-kva.de")]
    [InlineData("localhost", null)]
    [InlineData("192.168.1.1", null)]
    [InlineData("not a site", null)]
    [InlineData("-bad-.com", null)]
    [InlineData("", null)]
    [InlineData("evil.com\r\n127.0.0.1 bank.com", null)]
    public void Normalizes_domains(string input, string? expected)
    {
        Assert.Equal(expected, HostsFile.NormalizeDomain(input));
    }
}

public class BlockListTests
{
    [Theory]
    [InlineData("Discord.exe", "Discord")]
    [InlineData(" steam ", "steam")]
    [InlineData(@"C:\Games\Fortnite\FortniteClient-Win64-Shipping.exe", "FortniteClient-Win64-Shipping")]
    [InlineData("", null)]
    [InlineData("bad|name", null)]
    public void Normalizes_app_names(string input, string? expected)
    {
        Assert.Equal(expected, BlockList.NormalizeAppName(input));
    }

    [Theory]
    [InlineData("explorer")]
    [InlineData("Explorer.exe")]
    [InlineData("FocusBar")]
    [InlineData("msedgewebview2")]
    [InlineData("svchost")]
    public void Windows_and_FocusBar_programs_are_protected(string name)
    {
        Assert.True(BlockList.IsProtected(name));
    }

    [Fact]
    public void Matches_running_processes_ignoring_case_and_exe()
    {
        var settings = FocusSettings.Default with { BlockedApps = ["Discord.exe", "steam"] };
        Assert.True(BlockList.IsBlocked("discord", settings));
        Assert.True(BlockList.IsBlocked("Steam", settings));
        Assert.False(BlockList.IsBlocked("notepad", settings));
    }

    [Fact]
    public void Protected_programs_are_never_blocked_even_if_listed()
    {
        var settings = FocusSettings.Default with { BlockedApps = ["explorer"] };
        Assert.False(BlockList.IsBlocked("explorer", settings));
    }

    [Fact]
    public void Clean_removes_bad_duplicate_and_protected_entries_and_fixes_times()
    {
        var messy = new FocusSettings(true, TimeSpan.FromDays(30), (Strictness)42, TimeSpan.Zero,
            ["Discord.exe", "discord", "explorer", "", "Steam"],
            ["https://www.youtube.com/", "youtube.com", "nope", "Reddit.com"]);

        var clean = BlockList.Clean(messy);

        Assert.Equal(["Discord", "Steam"], clean.BlockedApps);
        Assert.Equal(["youtube.com", "reddit.com"], clean.BlockedSites);
        Assert.Equal(FocusSettings.Default.StartBeforeDeadline, clean.StartBeforeDeadline);
        Assert.Equal(FocusSettings.Default.SoftOverrideDelay, clean.SoftOverrideDelay);
        Assert.Equal(FocusSettings.Default.Strictness, clean.Strictness);
    }

    [Fact]
    public void Websites_are_blocked_in_soft_and_strict_but_not_gentle()
    {
        Assert.False(BlockList.BlocksWebsites(Strictness.Gentle));
        Assert.True(BlockList.BlocksWebsites(Strictness.Soft));
        Assert.True(BlockList.BlocksWebsites(Strictness.Strict));
    }
}

public sealed class FocusPlannerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 18, 0, 0, TimeSpan.FromHours(-4));
    private static readonly FocusSettings WithApps = FocusSettings.Default with { BlockedApps = ["Discord"] }; // auto, 2 h, Soft
    private static readonly HashSet<string> NoneDismissed = [];

    private readonly TempAppData _temp = new();

    public void Dispose() => _temp.Dispose();

    private static Deadline Due(DateTimeOffset at, bool complete = false, string title = "Essay") =>
        new(Guid.NewGuid(), title, null, null, at, DeadlineSource.Manual, null, complete);

    [Fact]
    public void Turns_on_two_hours_before_the_soonest_deadline_and_ends_when_it_is_due()
    {
        var soon = Due(Now.AddMinutes(90), title: "Soon");
        var later = Due(Now.AddMinutes(100), title: "Later");

        var session = FocusPlanner.Current(WithApps, [later, soon], null, NoneDismissed, Now);

        Assert.NotNull(session);
        Assert.Equal(FocusReason.Deadline, session.Reason);
        Assert.Equal("Soon", session.Deadline!.Title);
        Assert.Equal(soon.DueAt, session.EndsAt);
        Assert.Equal(soon.DueAt.AddHours(-2), session.StartedAt);
    }

    [Fact]
    public void Off_when_the_deadline_is_further_away_complete_or_past()
    {
        Assert.Null(FocusPlanner.Current(WithApps, [Due(Now.AddHours(3))], null, NoneDismissed, Now));
        Assert.Null(FocusPlanner.Current(WithApps, [Due(Now.AddHours(1), complete: true)], null, NoneDismissed, Now));
        Assert.Null(FocusPlanner.Current(WithApps, [Due(Now.AddMinutes(-1))], null, NoneDismissed, Now));
    }

    [Fact]
    public void Automatic_needs_auto_enabled_and_something_to_block()
    {
        var deadline = Due(Now.AddHours(1));
        Assert.Null(FocusPlanner.Current(WithApps with { AutoEnable = false }, [deadline], null, NoneDismissed, Now));
        Assert.Null(FocusPlanner.Current(FocusSettings.Default, [deadline], null, NoneDismissed, Now));

        // Websites alone count, except in Gentle where websites aren't blocked.
        var sitesOnly = FocusSettings.Default with { BlockedSites = ["youtube.com"] };
        Assert.NotNull(FocusPlanner.Current(sitesOnly, [deadline], null, NoneDismissed, Now));
        Assert.Null(FocusPlanner.Current(sitesOnly with { Strictness = Strictness.Gentle }, [deadline], null, NoneDismissed, Now));
    }

    [Fact]
    public void A_manual_session_runs_until_its_timer_ends()
    {
        var manual = new ManualSession(Now, Now.AddMinutes(25));

        var session = FocusPlanner.Current(FocusSettings.Default, [], manual, NoneDismissed, Now.AddMinutes(10));
        Assert.Equal(FocusReason.Manual, session!.Reason);
        Assert.Equal(Now.AddMinutes(25), session.EndsAt);

        Assert.Null(FocusPlanner.Current(FocusSettings.Default, [], manual, NoneDismissed, Now.AddMinutes(25)));
    }

    [Fact]
    public void A_manual_session_takes_priority_over_an_automatic_one()
    {
        var manual = new ManualSession(Now, Now.AddMinutes(50));
        var session = FocusPlanner.Current(WithApps, [Due(Now.AddHours(1))], manual, NoneDismissed, Now);
        Assert.Equal(FocusReason.Manual, session!.Reason);
    }

    [Fact]
    public void Ending_early_turns_it_off_for_that_deadline_only()
    {
        var first = Due(Now.AddMinutes(30), title: "First");
        var second = Due(Now.AddMinutes(60), title: "Second");
        var dismissed = new HashSet<string> { FocusPlanner.DismissKey(first) };

        var session = FocusPlanner.Current(WithApps, [first, second], null, dismissed, Now);

        Assert.Equal("Second", session!.Deadline!.Title);
    }

    [Fact]
    public void Moving_the_deadline_brings_automatic_focus_back()
    {
        var deadline = Due(Now.AddMinutes(30));
        var dismissed = new HashSet<string> { FocusPlanner.DismissKey(deadline) };
        var moved = deadline with { DueAt = Now.AddMinutes(45) };

        Assert.NotNull(FocusPlanner.Current(WithApps, [moved], null, dismissed, Now));
    }

    [Fact]
    public void Strict_ignores_early_endings_and_cannot_be_ended()
    {
        var strict = WithApps with { Strictness = Strictness.Strict };
        var deadline = Due(Now.AddMinutes(30));
        var dismissed = new HashSet<string> { FocusPlanner.DismissKey(deadline) };

        var session = FocusPlanner.Current(strict, [deadline], null, dismissed, Now);

        Assert.NotNull(session);
        Assert.False(FocusPlanner.CanEndEarly(session, strict));
        Assert.True(FocusPlanner.CanEndEarly(session, WithApps));
    }

    [Fact]
    public void State_round_trips()
    {
        var store = new FocusStateStore(new JsonFileStore(_temp.Paths));
        Assert.Null(store.Load().Manual);

        var state = new FocusState { Manual = new ManualSession(Now, Now.AddMinutes(50)), Dismissed = ["a|1"] };
        store.Save(state);
        var loaded = store.Load();

        Assert.Equal(state.Manual, loaded.Manual);
        Assert.Equal(["a|1"], loaded.Dismissed);
    }

    [Fact]
    public void Focus_settings_are_saved_and_cleaned_with_the_other_settings()
    {
        var store = new SettingsStore(new JsonFileStore(_temp.Paths));
        Assert.Equal(FocusSettings.Default.StartBeforeDeadline, store.Load().Focus.StartBeforeDeadline);

        store.Save(AppSettings.Default with
        {
            Focus = FocusSettings.Default with { Strictness = Strictness.Strict, BlockedApps = ["Discord.exe", "explorer"], BlockedSites = ["www.youtube.com"] },
        });
        var loaded = store.Load().Focus;

        Assert.Equal(Strictness.Strict, loaded.Strictness);
        Assert.Equal(["Discord"], loaded.BlockedApps);
        Assert.Equal(["youtube.com"], loaded.BlockedSites);
    }
}
