using FocusBar.Core.Models;
using FocusBar.Core.Security;
using FocusBar.Core.Sources;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Tests;

public sealed class IcsCalendarSourceTests : IDisposable
{
    // A fixed UTC-4 zone so expectations don't depend on the machine running the tests.
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4");
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.FromHours(-4));
    private static readonly DateRange Window = DateRange.Around(Now, 7, 60);

    private const string Ics = """
        BEGIN:VCALENDAR
        VERSION:2.0
        PRODID:-//Test//EN
        X-WR-CALNAME:Biology
        BEGIN:VEVENT
        UID:utc-event@test
        SUMMARY:Lab report due
        DESCRIPTION:Use the template\, include graphs
        DTSTART:20261031T035900Z
        DTEND:20261031T040000Z
        END:VEVENT
        BEGIN:VEVENT
        UID:all-day@test
        SUMMARY:Field trip form
        DTSTART;VALUE=DATE:20261102
        DTEND;VALUE=DATE:20261103
        CATEGORIES:Homeroom
        END:VEVENT
        BEGIN:VEVENT
        UID:floating@test
        SUMMARY:Quiz
        DTSTART:20261103T090000
        END:VEVENT
        BEGIN:VEVENT
        UID:weekly@test
        SUMMARY:Soccer practice
        DTSTART:20261026T153000
        RRULE:FREQ=WEEKLY;BYDAY=MO
        END:VEVENT
        BEGIN:VEVENT
        UID:cancelled@test
        SUMMARY:Cancelled thing
        STATUS:CANCELLED
        DTSTART:20261104T090000
        END:VEVENT
        BEGIN:VEVENT
        UID:far@test
        SUMMARY:Next year's project
        DTSTART:20270601T090000
        END:VEVENT
        BEGIN:VEVENT
        SUMMARY:No UID event
        DTSTART:20261105T120000
        END:VEVENT
        BEGIN:VTODO
        UID:todo-done@test
        SUMMARY:Return library book
        DUE:20261027T170000
        STATUS:COMPLETED
        END:VTODO
        BEGIN:VTODO
        UID:todo-open@test
        SUMMARY:Sign permission slip
        DUE;VALUE=DATE:20261028
        END:VTODO
        END:VCALENDAR
        """;

    private readonly TempAppData _temp = new();

    public void Dispose() => _temp.Dispose();

    private static IReadOnlyList<Deadline> Parse(string? defaultClass = null) =>
        IcsCalendarSource.Parse(Ics, defaultClass, Window, Eastern);

    private static Deadline One(string uid) => Assert.Single(Parse(), d => d.ExternalId == uid);

    [Fact]
    public void Reads_events_and_tasks_skipping_repeating_cancelled_and_out_of_range()
    {
        var titles = Parse().Select(d => d.Title).Order().ToList();
        Assert.Equal(
            ["Field trip form", "Lab report due", "No UID event", "Quiz", "Return library book", "Sign permission slip"],
            titles);
    }

    [Fact]
    public void Utc_times_are_converted_to_local_time()
    {
        var lab = One("utc-event@test");
        Assert.Equal(new DateTimeOffset(2026, 10, 30, 23, 59, 0, TimeSpan.FromHours(-4)), lab.DueAt);
        Assert.Equal("Use the template, include graphs", lab.Description);
        Assert.Equal(DeadlineSource.Calendar, lab.Source);
    }

    [Fact]
    public void All_day_events_are_due_at_11_59_pm()
    {
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 23, 59, 0, TimeSpan.FromHours(-4)), One("all-day@test").DueAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 23, 59, 0, TimeSpan.FromHours(-4)), One("todo-open@test").DueAt);
    }

    [Fact]
    public void Floating_times_are_read_as_local_time()
    {
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.FromHours(-4)), One("floating@test").DueAt);
    }

    [Fact]
    public void Completed_tasks_come_in_as_complete()
    {
        Assert.True(One("todo-done@test").IsComplete);
        Assert.False(One("todo-open@test").IsComplete);
    }

    [Fact]
    public void Class_comes_from_categories_then_the_feeds_class()
    {
        var parsed = IcsCalendarSource.Parse(Ics, "Biology", Window, Eastern);
        Assert.Equal("Homeroom", parsed.Single(d => d.ExternalId == "all-day@test").ClassName);
        Assert.Equal("Biology", parsed.Single(d => d.ExternalId == "floating@test").ClassName);
    }

    [Fact]
    public void Events_without_a_uid_get_a_stable_one()
    {
        var first = Parse().Single(d => d.Title == "No UID event").ExternalId;
        var second = Parse().Single(d => d.Title == "No UID event").ExternalId;
        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.Equal(first, second);
    }

    [Fact]
    public void Not_a_calendar_throws()
    {
        Assert.ThrowsAny<Exception>(() => IcsCalendarSource.Parse("<html>nope</html>", null, Window, Eastern));
    }

    [Fact]
    public async Task Reads_a_local_file()
    {
        Directory.CreateDirectory(_temp.Paths.Root);
        var path = _temp.Paths.File("class.ics");
        await File.WriteAllTextAsync(path, Ics);

        var source = new IcsCalendarSource(new CalendarFeed("Biology", path), null, () => Now);
        var deadlines = await source.FetchDeadlinesAsync(CancellationToken.None);

        Assert.NotEmpty(deadlines);
        Assert.Equal(DateRange.Around(Now, 7, 60), source.Covers);
        Assert.Empty(await source.FetchScheduleAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Reads_a_webcal_link_over_https()
    {
        var handler = new FakeHttpHandler(request => FakeHttpHandler.Text(Ics, "text/calendar"));
        using var http = new HttpClient(handler);
        var source = new IcsCalendarSource(new CalendarFeed("Bio", "webcal://calendar.example.com/feed.ics"), http, () => Now);

        await source.FetchDeadlinesAsync(CancellationToken.None);

        Assert.Equal("https://calendar.example.com/feed.ics", handler.Requests.Single().Url);
    }

    [Theory]
    [InlineData("https://calendar.google.com/x.ics", true)]
    [InlineData("webcal://x.example.com/c.ics", true)]
    [InlineData(@"C:\Users\me\Downloads\class.ics", false)]
    public void Knows_links_from_files(string location, bool isUrl)
    {
        Assert.Equal(isUrl, new CalendarFeed("x", location).IsUrl);
    }

    [Fact]
    public void Feed_key_is_stable_and_ignores_case()
    {
        var a = new CalendarFeed("A", "https://Example.com/Cal.ics").Key;
        var b = new CalendarFeed("B", "https://example.com/cal.ics").Key;
        Assert.Equal(a, b);
        Assert.StartsWith("calendar-", a);
        Assert.NotEqual(a, new CalendarFeed("A", "https://example.com/other.ics").Key);
    }

    [Fact]
    public void Feed_store_encrypts_links_and_round_trips()
    {
        var store = new CalendarFeedStore(new JsonFileStore(_temp.Paths), new FakeProtector());
        var secret = "https://calendar.google.com/calendar/ical/private-abc123/basic.ics";
        store.Save([new CalendarFeed("Biology", secret, "Biology"), new CalendarFeed("File", @"C:\cal.ics")]);

        Assert.DoesNotContain("private-abc123", File.ReadAllText(_temp.Paths.File(CalendarFeedStore.FileName)));
        Assert.Equal([new CalendarFeed("Biology", secret, "Biology"), new CalendarFeed("File", @"C:\cal.ics")], store.Load());
    }

    [Fact]
    public void Feed_store_skips_entries_it_cannot_decrypt()
    {
        var paths = _temp.Paths;
        new CalendarFeedStore(new JsonFileStore(paths), new FakeProtector()).Save([new CalendarFeed("A", "https://a.example/c.ics")]);
        var otherUser = new CalendarFeedStore(new JsonFileStore(paths), new FakeProtector(fail: true));
        Assert.Empty(otherUser.Load());
    }
}

/// <summary>Stands in for Windows DPAPI: reversible, but the plain text never appears in the output.</summary>
internal sealed class FakeProtector(bool fail = false) : ISecretProtector
{
    public string Protect(string plainText) =>
        "enc:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(new string(plainText.Reverse().ToArray())));

    public string Unprotect(string protectedText)
    {
        if (fail || !protectedText.StartsWith("enc:")) throw new System.Security.Cryptography.CryptographicException("Can't decrypt.");
        return new string(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedText[4..])).Reverse().ToArray());
    }
}
