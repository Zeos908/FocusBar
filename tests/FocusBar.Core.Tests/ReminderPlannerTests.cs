using FocusBar.Core.Models;
using FocusBar.Core.Reminders;
using FocusBar.Core.Settings;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Tests;

public sealed class ReminderPlannerTests : IDisposable
{
    private static readonly DateTimeOffset Monday9am = new(2026, 10, 26, 9, 0, 0, TimeSpan.FromHours(-4));
    private static readonly ReminderSettings Defaults = ReminderSettings.Default; // 1 day, 3 hours, 1 hour

    private readonly TempAppData _temp = new();

    public void Dispose() => _temp.Dispose();

    private static Deadline Due(DateTimeOffset dueAt, string title = "Bio lab", bool complete = false) =>
        new(Guid.NewGuid(), title, "Biology", null, dueAt, DeadlineSource.Manual, null, complete);

    /// <summary>Runs the planner every 30 seconds from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static List<(DateTimeOffset At, Reminder Reminder)> Simulate(
        IReadOnlyList<Deadline> deadlines, ReminderState state, DateTimeOffset from, DateTimeOffset to, ReminderSettings? settings = null)
    {
        var shown = new List<(DateTimeOffset, Reminder)>();
        for (var now = from; now <= to; now += TimeSpan.FromSeconds(30))
        {
            foreach (var r in ReminderPlanner.Due(deadlines, settings ?? Defaults, state, now)) shown.Add((now, r));
        }
        return shown;
    }

    [Fact]
    public void Shows_each_reminder_once_at_its_time()
    {
        var friday = Due(Monday9am.AddDays(4));
        var state = new ReminderState();

        var shown = Simulate([friday], state, Monday9am, friday.DueAt.AddHours(1));

        Assert.Equal([TimeSpan.FromDays(1), TimeSpan.FromHours(3), TimeSpan.FromHours(1)], shown.Select(s => s.Reminder.Offset));
        Assert.Equal(friday.DueAt - TimeSpan.FromDays(1), shown[0].At);
        Assert.All(shown, s => Assert.False(s.Reminder.IsCatchUp));
    }

    [Fact]
    public void Survives_a_restart_without_repeating()
    {
        var friday = Due(Monday9am.AddDays(4));
        var state = new ReminderState();
        var dayBefore = friday.DueAt - TimeSpan.FromDays(1);

        Simulate([friday], state, Monday9am, dayBefore);
        var store = new ReminderStateStore(new JsonFileStore(_temp.Paths));
        store.Save(state);
        var reloaded = store.Load();

        var shownAfterRestart = Simulate([friday], reloaded, dayBefore, dayBefore.AddMinutes(10));
        Assert.Empty(shownAfterRestart);
    }

    [Fact]
    public void After_being_off_shows_only_one_catch_up_per_deadline()
    {
        var friday = Due(Monday9am.AddDays(4));
        var state = new ReminderState();
        ReminderPlanner.Due([friday], Defaults, state, Monday9am); // seen on Monday

        // PC off from Monday until 30 minutes before it's due: the 1-day, 3-hour and 1-hour reminders all passed.
        var back = friday.DueAt.AddMinutes(-30);
        var shown = ReminderPlanner.Due([friday], Defaults, state, back);

        var only = Assert.Single(shown);
        Assert.Equal(TimeSpan.FromHours(1), only.Offset);
        Assert.True(only.IsCatchUp);
        Assert.Empty(ReminderPlanner.Due([friday], Defaults, state, back.AddMinutes(1)));
    }

    [Fact]
    public void A_new_deadline_does_not_fire_reminders_that_were_already_past()
    {
        var state = new ReminderState();
        var inTwoHours = Due(Monday9am.AddHours(2));

        var shown = Simulate([inTwoHours], state, Monday9am, inTwoHours.DueAt);

        // 1 day and 3 hours before were already past when it was added; only "1 hour before" shows.
        Assert.Equal(TimeSpan.FromHours(1), Assert.Single(shown).Reminder.Offset);
    }

    [Fact]
    public void A_deadline_added_seconds_before_a_reminder_time_still_gets_it()
    {
        var state = new ReminderState();
        var dueInAnHour = Due(Monday9am.AddHours(1).AddSeconds(20));

        var shown = Simulate([dueInAnHour], state, Monday9am, Monday9am.AddMinutes(5));

        Assert.Equal(TimeSpan.FromHours(1), Assert.Single(shown).Reminder.Offset);
    }

    [Fact]
    public void Nothing_for_completed_or_past_deadlines()
    {
        var state = new ReminderState();
        var done = Due(Monday9am.AddHours(2), complete: true);
        var overdue = Due(Monday9am.AddHours(-1));

        Assert.Empty(Simulate([done, overdue], state, Monday9am, Monday9am.AddHours(3)));
    }

    [Fact]
    public void Changing_the_due_date_sets_up_fresh_reminders()
    {
        var state = new ReminderState();
        var original = Due(Monday9am.AddHours(2));
        Simulate([original], state, Monday9am, Monday9am.AddHours(1).AddMinutes(1)); // "1 hour before" shown

        var moved = original with { DueAt = Monday9am.AddDays(2) };
        var shown = Simulate([moved], state, Monday9am.AddHours(2), moved.DueAt);

        Assert.Equal([TimeSpan.FromDays(1), TimeSpan.FromHours(3), TimeSpan.FromHours(1)], shown.Select(s => s.Reminder.Offset));
    }

    [Fact]
    public void Snooze_brings_the_reminder_back_once()
    {
        var state = new ReminderState();
        var deadline = Due(Monday9am.AddHours(5));
        ReminderPlanner.Due([deadline], Defaults, state, Monday9am);

        state.Snooze(deadline.Id, Monday9am.AddMinutes(15));
        var shown = Simulate([deadline], state, Monday9am.AddMinutes(1), Monday9am.AddMinutes(30));

        var back = Assert.Single(shown);
        Assert.Null(back.Reminder.Offset);
        Assert.Equal(Monday9am.AddMinutes(15), back.At);
        Assert.Empty(state.Snoozed);
    }

    [Fact]
    public void Completing_a_deadline_cancels_its_snooze()
    {
        var state = new ReminderState();
        var deadline = Due(Monday9am.AddHours(5));
        state.Snooze(deadline.Id, Monday9am.AddMinutes(15));

        var shown = Simulate([deadline with { IsComplete = true }], state, Monday9am, Monday9am.AddMinutes(30));

        Assert.Empty(shown);
        Assert.Empty(state.Snoozed);
    }

    [Fact]
    public void Turned_off_shows_nothing_and_does_not_pile_up_for_later()
    {
        var state = new ReminderState();
        var deadline = Due(Monday9am.AddDays(2));
        var off = Defaults with { Enabled = false };

        Assert.Empty(Simulate([deadline], state, Monday9am, Monday9am.AddDays(1).AddHours(1), off));

        // Turned back on: earlier reminders don't all come flooding in.
        var shown = Simulate([deadline], state, Monday9am.AddDays(1).AddHours(1), deadline.DueAt);
        Assert.Equal([TimeSpan.FromHours(3), TimeSpan.FromHours(1)], shown.Select(s => s.Reminder.Offset));
    }

    [Fact]
    public void Custom_offsets_are_used()
    {
        var state = new ReminderState();
        var deadline = Due(Monday9am.AddDays(10));
        var custom = new ReminderSettings(true, [TimeSpan.FromDays(7), TimeSpan.FromMinutes(15)]);

        var shown = Simulate([deadline], state, Monday9am, deadline.DueAt, custom);

        Assert.Equal([TimeSpan.FromDays(7), TimeSpan.FromMinutes(15)], shown.Select(s => s.Reminder.Offset));
    }

    [Fact]
    public void Prune_forgets_deleted_deadlines()
    {
        var state = new ReminderState();
        var kept = Due(Monday9am.AddMinutes(30));
        var deleted = Due(Monday9am.AddMinutes(30));
        ReminderPlanner.Due([kept, deleted], Defaults, state, Monday9am);
        state.Snooze(deleted.Id, Monday9am.AddHours(1));

        Assert.True(state.Prune(new HashSet<Guid> { kept.Id }));

        Assert.Equal([kept.Id], state.FirstSeen.Keys);
        Assert.Empty(state.Snoozed);
        Assert.All(state.Fired, key => Assert.StartsWith(kept.Id.ToString("N"), key));
        Assert.False(state.Prune(new HashSet<Guid> { kept.Id }));
    }

    [Fact]
    public void Headlines()
    {
        var deadline = Due(Monday9am.AddHours(3));
        Assert.Equal("Bio lab is due in 3 hours", ReminderPlanner.Headline(deadline, Monday9am));
        Assert.Equal("Bio lab is due now", ReminderPlanner.Headline(deadline, deadline.DueAt));
        Assert.Equal("Bio lab was due 10 minutes ago", ReminderPlanner.Headline(deadline, deadline.DueAt.AddMinutes(10)));
    }

    [Theory]
    [InlineData(7 * 24 * 60, "1 week")]
    [InlineData(2 * 24 * 60, "2 days")]
    [InlineData(24 * 60, "1 day")]
    [InlineData(180, "3 hours")]
    [InlineData(30, "30 minutes")]
    public void Describes_offsets(int minutes, string expected)
    {
        Assert.Equal(expected, ReminderSettings.Describe(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void Settings_round_trip_and_fill_in_missing_parts()
    {
        var store = new SettingsStore(new JsonFileStore(_temp.Paths));
        Assert.Equal(ReminderSettings.Default.Offsets, store.Load().Reminders.Offsets);

        store.Save(new AppSettings(new ReminderSettings(false, [TimeSpan.FromMinutes(15), TimeSpan.FromDays(2), TimeSpan.FromDays(2)])));
        var loaded = store.Load();
        Assert.False(loaded.Reminders.Enabled);
        Assert.Equal([TimeSpan.FromDays(2), TimeSpan.FromMinutes(15)], loaded.Reminders.Offsets);

        File.WriteAllText(_temp.Paths.File(AppSettings.FileName), "{}");
        Assert.Equal(ReminderSettings.Default.Offsets, store.Load().Reminders.Offsets);
    }
}
