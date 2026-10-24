using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;
using FocusBar.Core.Sources;

namespace FocusBar.Core.Tests;

public class DeadlineMergeTests
{
    private static readonly DateTimeOffset Friday = new(2026, 10, 30, 23, 59, 0, TimeSpan.FromHours(-4));
    private static readonly DateRange Window = new(Friday.AddDays(-14), Friday.AddDays(30));

    private static Deadline School(string id, string title, DateTimeOffset due, bool done = false, string? className = "Biology") =>
        new(Guid.Empty, title, className, null, due, DeadlineSource.SchoolApi, id, done);

    private static Deadline Calendar(string id, string title, DateTimeOffset due) =>
        new(Guid.Empty, title, null, null, due, DeadlineSource.Calendar, id, false);

    // ----- Normalizing titles -----

    [Theory]
    [InlineData("Ch. 7 Reading — Questions!", "ch 7 reading questions")]
    [InlineData("  LAB   report ", "lab report")]
    [InlineData("Lab-report", "lab report")]
    [InlineData("Capítulo 3", "capítulo 3")]
    public void Normalizes_titles(string title, string expected)
    {
        Assert.Equal(expected, DeadlineMerger.NormalizeTitle(title));
    }

    // ----- Applying a sync -----

    [Fact]
    public void Sync_adds_new_deadlines_with_their_own_ids()
    {
        var list = new DeadlineList();
        var changes = list.ApplySync("school", DeadlineSource.SchoolApi, [School("9001", "Lab report", Friday)], Window);

        Assert.Equal(new SyncChanges(1, 0, 0), changes);
        var added = Assert.Single(list.All);
        Assert.NotEqual(Guid.Empty, added.Id);
        Assert.Equal("school:9001", added.ExternalId);
        Assert.Equal(DeadlineSource.SchoolApi, added.Source);
    }

    [Fact]
    public void Sync_updates_known_deadlines_and_keeps_their_id()
    {
        var list = new DeadlineList();
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("9001", "Lab report", Friday)], Window);
        var id = list.All[0].Id;

        var changes = list.ApplySync("school", DeadlineSource.SchoolApi, [School("9001", "Lab report (revised)", Friday.AddDays(1))], Window);

        Assert.Equal(new SyncChanges(0, 1, 0), changes);
        var updated = Assert.Single(list.All);
        Assert.Equal(id, updated.Id);
        Assert.Equal("Lab report (revised)", updated.Title);
        Assert.Equal(Friday.AddDays(1), updated.DueAt);
    }

    [Fact]
    public void Unchanged_sync_reports_no_changes()
    {
        var list = new DeadlineList();
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("9001", "Lab report", Friday)], Window);
        Assert.False(list.ApplySync("school", DeadlineSource.SchoolApi, [School("9001", "Lab report", Friday)], Window).Any);
    }

    [Fact]
    public void Sync_removes_deadlines_the_source_dropped_but_only_inside_its_date_range()
    {
        var list = new DeadlineList();
        var farFuture = Friday.AddDays(100);
        list.ApplySync("school", DeadlineSource.SchoolApi,
            [School("1", "Kept", Friday), School("2", "Deleted by teacher", Friday), School("3", "Outside range", farFuture)],
            new DateRange(Friday.AddDays(-14), farFuture));

        var changes = list.ApplySync("school", DeadlineSource.SchoolApi, [School("1", "Kept", Friday)], Window);

        Assert.Equal(1, changes.Removed);
        Assert.Equal(["Kept", "Outside range"], list.All.Select(d => d.Title).Order());
    }

    [Fact]
    public void Sync_never_touches_manual_deadlines_or_other_sources()
    {
        var list = new DeadlineList();
        list.AddManual("My own task", null, null, Friday);
        list.ApplySync("calendar-1", DeadlineSource.Calendar, [Calendar("uid-1", "Calendar event", Friday)], Window);

        list.ApplySync("school", DeadlineSource.SchoolApi, [], Window);
        list.ApplySync("calendar-2", DeadlineSource.Calendar, [], Window);

        Assert.Equal(2, list.All.Count);
    }

    [Fact]
    public void Completing_in_FocusBar_sticks_even_if_the_school_still_says_to_do()
    {
        var list = new DeadlineList();
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("9001", "Lab report", Friday)], Window);
        list.SetComplete(list.All[0].Id, true);

        list.ApplySync("school", DeadlineSource.SchoolApi, [School("9001", "Lab report", Friday, done: false)], Window);

        Assert.True(list.All[0].IsComplete);
    }

    [Fact]
    public void Completed_at_school_marks_it_complete_here()
    {
        var list = new DeadlineList();
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("9001", "Lab report", Friday)], Window);
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("9001", "Lab report", Friday, done: true)], Window);
        Assert.True(list.All[0].IsComplete);
    }

    [Fact]
    public void Sync_skips_items_without_an_id_or_title_and_repeated_ids()
    {
        var list = new DeadlineList();
        var changes = list.ApplySync("school", DeadlineSource.SchoolApi,
            [School("", "No id", Friday), School("1", "  ", Friday), School("2", "Twice", Friday), School("2", "Twice again", Friday)],
            Window);

        Assert.Equal(1, changes.Added);
        Assert.Equal("Twice", Assert.Single(list.All).Title);
    }

    [Fact]
    public void Manual_deadlines_cannot_be_synced()
    {
        Assert.Throws<ArgumentException>(() => new DeadlineList().ApplySync("x", DeadlineSource.Manual, [], Window));
    }

    [Fact]
    public void RemoveSource_removes_only_that_source()
    {
        var list = new DeadlineList();
        list.AddManual("Mine", null, null, Friday);
        list.ApplySync("school-demo", DeadlineSource.SchoolApi, [School("1", "Demo", Friday)], Window);
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("1", "Real", Friday)], Window);

        Assert.Equal(1, list.RemoveSource("school-demo"));
        Assert.Equal(["Mine", "Real"], list.All.Select(d => d.Title).Order());
    }

    // ----- Duplicates across sources -----

    [Fact]
    public void Same_assignment_typed_in_and_synced_shows_once_preferring_the_manual_one()
    {
        var list = new DeadlineList();
        var mine = list.AddManual("Lab Report!", "Bio", "my notes", Friday);
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("9001", "lab report", Friday.AddMinutes(-30))], Window);

        var shown = Assert.Single(list.Upcoming());
        Assert.Equal(mine.Id, shown.Id);
        Assert.Equal(2, list.All.Count); // nothing deleted, just folded together
    }

    [Fact]
    public void School_beats_calendar_when_there_is_no_manual_entry()
    {
        var list = new DeadlineList();
        list.ApplySync("calendar-1", DeadlineSource.Calendar, [Calendar("uid", "Unit 4 test", Friday)], Window);
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("9002", "Unit 4 Test", Friday)], Window);

        Assert.Equal(DeadlineSource.SchoolApi, Assert.Single(list.Upcoming()).Source);
    }

    [Fact]
    public void More_than_an_hour_apart_is_not_a_duplicate()
    {
        var list = new DeadlineList();
        list.AddManual("Quiz", null, null, Friday);
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("1", "Quiz", Friday.AddMinutes(61))], Window);
        Assert.Equal(2, list.Upcoming().Count);
    }

    [Fact]
    public void Exactly_an_hour_apart_is_a_duplicate()
    {
        var list = new DeadlineList();
        list.AddManual("Quiz", null, null, Friday);
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("1", "Quiz", Friday.AddHours(1))], Window);
        Assert.Single(list.Upcoming());
    }

    [Fact]
    public void Two_manual_entries_or_two_items_from_one_source_are_never_merged()
    {
        var list = new DeadlineList();
        list.AddManual("Reading", null, null, Friday);
        list.AddManual("Reading", null, null, Friday);
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("1", "Worksheet", Friday), School("2", "Worksheet", Friday)], Window);

        Assert.Equal(4, list.Upcoming().Count);
    }

    [Fact]
    public void The_same_event_in_two_calendars_shows_once()
    {
        var list = new DeadlineList();
        list.ApplySync("calendar-1", DeadlineSource.Calendar, [Calendar("a", "Field trip form", Friday)], Window);
        list.ApplySync("calendar-2", DeadlineSource.Calendar, [Calendar("b", "Field trip form", Friday)], Window);
        Assert.Single(list.Upcoming());
    }

    [Fact]
    public void Completing_the_shown_copy_completes_its_duplicates_so_they_do_not_reappear()
    {
        var list = new DeadlineList();
        var mine = list.AddManual("Essay", null, null, Friday);
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("1", "Essay", Friday)], Window);

        Assert.True(list.SetComplete(mine.Id, true));

        Assert.Empty(list.Upcoming());
        Assert.Single(list.Completed());
        Assert.All(list.All, d => Assert.True(d.IsComplete));

        list.SetComplete(mine.Id, false);
        Assert.Single(list.Upcoming());
    }

    [Fact]
    public void Done_at_school_shows_the_merged_deadline_as_complete()
    {
        var list = new DeadlineList();
        list.AddManual("Essay", null, null, Friday);
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("1", "Essay", Friday, done: true)], Window);

        Assert.Empty(list.Upcoming());
        Assert.Equal(DeadlineSource.Manual, Assert.Single(list.Completed()).Source);
    }

    [Fact]
    public void Deleting_the_manual_copy_lets_the_synced_one_show_again()
    {
        var list = new DeadlineList();
        var mine = list.AddManual("Essay", null, null, Friday);
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("1", "Essay", Friday)], Window);

        list.Remove(mine.Id);

        Assert.Equal(DeadlineSource.SchoolApi, Assert.Single(list.Upcoming()).Source);
    }

    [Fact]
    public void Merged_view_has_every_deadline_once()
    {
        var list = new DeadlineList();
        list.AddManual("Essay", null, null, Friday);
        list.AddManual("Quiz", null, null, Friday);
        list.ApplySync("school", DeadlineSource.SchoolApi, [School("1", "Essay", Friday), School("2", "Lab", Friday)], Window);

        Assert.Equal(["Essay", "Lab", "Quiz"], list.Merged().Select(d => d.Title).Order());
    }
}
