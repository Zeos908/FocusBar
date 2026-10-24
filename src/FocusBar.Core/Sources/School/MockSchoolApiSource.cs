using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;

namespace FocusBar.Core.Sources.School;

/// <summary>
/// Made-up but realistic school data: a week of assignments around today and a 7-period weekday schedule.
/// Lets anyone (including judges without a school account) see the school sync working.
/// The assignments are built fresh each day relative to that day.
/// </summary>
public sealed class MockSchoolApiSource : IDeadlineSource
{
    public const string SourceKey = "school-demo";

    private readonly Func<DateTimeOffset> _now;
    private readonly TimeZoneInfo _zone;

    public MockSchoolApiSource(Func<DateTimeOffset>? now = null, TimeZoneInfo? zone = null)
    {
        _now = now ?? (() => DateTimeOffset.Now);
        _zone = zone ?? TimeZoneInfo.Local;
    }

    public string Key => SourceKey;
    public string Name => "Demo school data";
    public DeadlineSource Kind => DeadlineSource.SchoolApi;
    public DateRange Covers { get; private set; }

    public Task<IReadOnlyList<Deadline>> FetchDeadlinesAsync(CancellationToken ct)
    {
        var now = _now();
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        Covers = DateRange.Around(now, 7, 60);

        Deadline Item(int n, string title, string className, int days, int hour, int minute, string? type, bool done = false) =>
            new(Guid.Empty, title, className, type, DueInput.Combine(today.AddDays(days), new TimeOnly(hour, minute), _zone),
                DeadlineSource.SchoolApi, $"demo-{today:yyyyMMdd}-{n}", done);

        IReadOnlyList<Deadline> items =
        [
            Item(1, "Problem set 12: Projectile motion", "Physics", 0, 23, 59, "Homework"),
            Item(2, "Read Chapter 7 and answer questions 1–5", "English 10", 1, 8, 0, "Reading"),
            Item(3, "Vocabulary quiz (Capítulo 3)", "Spanish II", 2, 10, 30, "Quiz"),
            Item(4, "Lab report: Enzyme activity", "Biology", 3, 23, 59, "Lab"),
            Item(5, "Unit 4 test: Quadratic functions", "Algebra II", 5, 9, 0, "Test"),
            Item(6, "Primary source analysis: Federalist No. 10", "US History", 8, 23, 59, "Essay"),
            Item(7, "Sketchbook check", "Studio Art", -1, 15, 0, "Classwork", done: true),
            Item(8, "Essay outline", "English 10", -1, 23, 59, "Homework"),
        ];
        return Task.FromResult(items);
    }

    public Task<IReadOnlyList<ClassPeriod>> FetchScheduleAsync(CancellationToken ct)
    {
        (string Name, int H1, int M1, int H2, int M2, string? Room)[] day =
        [
            ("Biology", 8, 0, 8, 50, "204"),
            ("Algebra II", 8, 55, 9, 45, "118"),
            ("English 10", 9, 50, 10, 40, "310"),
            ("Spanish II", 10, 45, 11, 35, "122"),
            ("US History", 12, 25, 13, 15, "215"),
            ("Physics", 13, 20, 14, 10, "Lab 2"),
            ("Studio Art", 14, 15, 15, 5, "Art Studio"),
        ];

        IReadOnlyList<ClassPeriod> week =
        [
            .. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
                .SelectMany(weekday => day.Select(c =>
                    new ClassPeriod(c.Name, weekday, new TimeOnly(c.H1, c.M1), new TimeOnly(c.H2, c.M2), c.Room))),
        ];
        return Task.FromResult(week);
    }
}
