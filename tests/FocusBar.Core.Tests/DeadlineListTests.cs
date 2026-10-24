using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Tests;

public sealed class DeadlineListTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 15, 0, 0, TimeSpan.FromHours(-4));

    private readonly TempAppData _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void AddManual_trims_text_and_drops_blank_optionals()
    {
        var list = new DeadlineList();
        var added = list.AddManual("  Bio lab  ", "   ", "\n", Now.AddDays(2));

        Assert.Equal("Bio lab", added.Title);
        Assert.Null(added.ClassName);
        Assert.Null(added.Description);
        Assert.Equal(DeadlineSource.Manual, added.Source);
        Assert.False(added.IsComplete);
        Assert.NotEqual(Guid.Empty, added.Id);
        Assert.Same(added, list.Find(added.Id));
    }

    [Fact]
    public void AddManual_requires_a_title()
    {
        var list = new DeadlineList();
        Assert.Throws<ArgumentException>(() => list.AddManual("   ", "Biology", null, Now));
        Assert.Empty(list.All);
    }

    [Fact]
    public void Long_text_is_cut_to_the_limit()
    {
        var list = new DeadlineList();
        var added = list.AddManual(new string('x', 500), new string('c', 500), null, Now);
        Assert.Equal(DeadlineList.MaxTitleLength, added.Title.Length);
        Assert.Equal(DeadlineList.MaxClassLength, added.ClassName!.Length);
    }

    [Fact]
    public void Upcoming_is_incomplete_soonest_first_including_overdue()
    {
        var list = new DeadlineList();
        var later = list.AddManual("Essay", "English", null, Now.AddDays(5));
        var overdue = list.AddManual("Worksheet", "Math", null, Now.AddDays(-1));
        var soon = list.AddManual("Quiz", "History", null, Now.AddHours(3));
        var done = list.AddManual("Reading", "English", null, Now.AddHours(1));
        list.SetComplete(done.Id, true);

        Assert.Equal([overdue.Id, soon.Id, later.Id], list.Upcoming().Select(d => d.Id));
        Assert.Equal([done.Id], list.Completed().Select(d => d.Id));
    }

    [Fact]
    public void Edit_changes_fields_but_keeps_identity_and_completion()
    {
        var list = new DeadlineList();
        var original = list.AddManual("Lab", "Bio", null, Now);
        list.SetComplete(original.Id, true);

        Assert.True(list.Edit(original.Id, "Lab report", " Biology ", "Pages 4-6", Now.AddDays(1)));

        var edited = list.Find(original.Id)!;
        Assert.Equal("Lab report", edited.Title);
        Assert.Equal("Biology", edited.ClassName);
        Assert.Equal("Pages 4-6", edited.Description);
        Assert.Equal(Now.AddDays(1), edited.DueAt);
        Assert.True(edited.IsComplete);
        Assert.Equal(DeadlineSource.Manual, edited.Source);
    }

    [Fact]
    public void Edit_or_complete_or_remove_a_missing_deadline_reports_false()
    {
        var list = new DeadlineList();
        var id = Guid.NewGuid();
        Assert.False(list.Edit(id, "x", null, null, Now));
        Assert.False(list.SetComplete(id, true));
        Assert.False(list.Remove(id));
    }

    [Fact]
    public void SetComplete_reports_whether_anything_changed()
    {
        var list = new DeadlineList();
        var d = list.AddManual("Lab", null, null, Now);
        Assert.True(list.SetComplete(d.Id, true));
        Assert.False(list.SetComplete(d.Id, true));
        Assert.True(list.SetComplete(d.Id, false));
    }

    [Fact]
    public void Remove_deletes()
    {
        var list = new DeadlineList();
        var d = list.AddManual("Lab", null, null, Now);
        Assert.True(list.Remove(d.Id));
        Assert.Empty(list.All);
    }

    [Fact]
    public void Recent_class_names_are_distinct_and_most_recent_first()
    {
        var list = new DeadlineList();
        list.AddManual("A", "Math", null, Now.AddDays(1));
        list.AddManual("B", "biology", null, Now.AddDays(3));
        list.AddManual("C", "Biology", null, Now.AddDays(2));
        list.AddManual("D", null, null, Now.AddDays(9));
        list.AddManual("E", "English", null, Now.AddDays(-5));

        Assert.Equal(["biology", "Math"], list.RecentClassNames(2));
        Assert.Equal(3, list.RecentClassNames(10).Count);
    }

    [Fact]
    public void Constructor_drops_blank_and_duplicate_entries()
    {
        var id = Guid.NewGuid();
        var list = new DeadlineList(
        [
            new Deadline(id, "Lab", null, null, Now, DeadlineSource.Manual, null, false),
            new Deadline(id, "Lab copy", null, null, Now, DeadlineSource.Manual, null, false),
            new Deadline(Guid.NewGuid(), " ", null, null, Now, DeadlineSource.Manual, null, false),
            new Deadline(Guid.Empty, "No id", null, null, Now, DeadlineSource.Manual, null, false),
        ]);

        Assert.Equal("Lab", Assert.Single(list.All).Title);
    }

    [Fact]
    public void Store_round_trips_deadlines_with_time_zone_offsets()
    {
        var store = new DeadlineStore(new JsonFileStore(_temp.Paths));
        var list = new DeadlineList();
        var d = list.AddManual("Essay", "English", "Five paragraphs", Now.AddDays(3));
        list.SetComplete(list.AddManual("Quiz", null, null, Now).Id, true);

        store.Save(list);
        var loaded = store.Load();

        Assert.Equal(list.All, loaded.All);
        Assert.Equal(TimeSpan.FromHours(-4), loaded.Find(d.Id)!.DueAt.Offset);
    }

    [Fact]
    public void Store_with_no_file_is_empty()
    {
        Assert.Empty(new DeadlineStore(new JsonFileStore(_temp.Paths)).Load().All);
    }
}
