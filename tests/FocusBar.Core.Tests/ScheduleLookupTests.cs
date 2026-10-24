using System.Globalization;
using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Tests;

public sealed class ScheduleLookupTests : IDisposable
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    // A school week: Biology then Math every weekday, plus Art on Fridays only.
    private static readonly ClassPeriod[] Schedule =
    [
        .. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            .SelectMany(day => new[]
            {
                new ClassPeriod("Biology", day, new TimeOnly(8, 0), new TimeOnly(8, 50), "204"),
                new ClassPeriod("Math", day, new TimeOnly(9, 0), new TimeOnly(9, 50), null),
            }),
        new ClassPeriod("Art", DayOfWeek.Friday, new TimeOnly(13, 0), new TimeOnly(13, 50), null),
    ];

    private readonly TempAppData _temp = new();

    public void Dispose() => _temp.Dispose();

    private static ClassAtTime? At(int year, int month, int day, int hour, int minute) =>
        ScheduleLookup.CurrentOrNext(Schedule, new DateTime(year, month, day, hour, minute, 0));

    [Fact]
    public void Empty_schedule_has_no_class()
    {
        Assert.Null(ScheduleLookup.CurrentOrNext([], new DateTime(2026, 10, 26, 8, 30, 0)));
    }

    [Fact]
    public void Finds_the_class_in_progress()
    {
        var result = At(2026, 10, 26, 8, 30); // Monday 8:30
        Assert.Equal("Biology", result!.Period.ClassName);
        Assert.True(result.IsNow);
    }

    [Fact]
    public void Class_end_time_counts_as_over()
    {
        var result = At(2026, 10, 26, 8, 50); // Monday 8:50, Biology just ended
        Assert.Equal("Math", result!.Period.ClassName);
        Assert.False(result.IsNow);
    }

    [Fact]
    public void Between_classes_finds_the_next_one_today()
    {
        var result = At(2026, 10, 30, 10, 0); // Friday 10:00
        Assert.Equal("Art", result!.Period.ClassName);
        Assert.Equal(new DateOnly(2026, 10, 30), result.StartsOn);
    }

    [Fact]
    public void After_school_on_Friday_finds_Monday_morning()
    {
        var result = At(2026, 10, 30, 15, 0); // Friday 3 PM
        Assert.Equal("Biology", result!.Period.ClassName);
        Assert.Equal(new DateOnly(2026, 11, 2), result.StartsOn);
    }

    [Fact]
    public void Describes_now_next_tomorrow_and_later()
    {
        var saturday = new DateOnly(2026, 10, 31);
        var monday = new DateOnly(2026, 11, 2);

        // Times are formatted by the region settings (which may use a special space before AM/PM).
        string T(int hour, int minute) => new TimeOnly(hour, minute).ToString("t", Us);

        Assert.Equal($"Now: Biology (204) · until {T(8, 50)}",
            ScheduleLookup.Describe(new ClassAtTime(Schedule[0], true, monday), monday, Us));
        Assert.Equal($"Next: Math · {T(9, 0)}",
            ScheduleLookup.Describe(new ClassAtTime(Schedule[1], false, monday), monday, Us));
        Assert.Equal($"Tomorrow: Math · {T(9, 0)}",
            ScheduleLookup.Describe(new ClassAtTime(Schedule[1], false, monday), monday.AddDays(-1), Us));
        Assert.Equal($"Mon: Biology (204) · {T(8, 0)}",
            ScheduleLookup.Describe(new ClassAtTime(Schedule[0], false, monday), saturday, Us));
    }

    [Fact]
    public void Store_round_trips_and_skips_invalid_periods()
    {
        var store = new ScheduleStore(new JsonFileStore(_temp.Paths));
        store.Save([.. Schedule, new ClassPeriod(" ", DayOfWeek.Monday, new TimeOnly(1, 0), new TimeOnly(2, 0), null),
                                 new ClassPeriod("Backwards", DayOfWeek.Monday, new TimeOnly(3, 0), new TimeOnly(2, 0), null)]);

        Assert.Equal(Schedule, store.Load());
    }
}
