using FocusBar.Core.Models;

namespace FocusBar.Core.Sources;

/// <summary>A span of due dates a source reported on.</summary>
public readonly record struct DateRange(DateTimeOffset From, DateTimeOffset To)
{
    public bool Contains(DateTimeOffset moment) => moment >= From && moment <= To;

    /// <summary>The range sources fetch by default: a week back (recently overdue) to two months ahead.</summary>
    public static DateRange Around(DateTimeOffset now, int daysBack, int daysAhead) =>
        new(now.Date.AddDays(-daysBack), now.Date.AddDays(daysAhead + 1).AddTicks(-1));
}

/// <summary>
/// Somewhere deadlines come from: the school website or a calendar (.ics). Manual entries don't need one.
/// This is the interface from the build instructions, plus three members the merge step needs.
/// </summary>
public interface IDeadlineSource
{
    /// <summary>Unique and stable, e.g. "school" or "calendar-3fa9c2d1". Keeps each source's deadlines separate.</summary>
    string Key { get; }

    /// <summary>Shown to the student, e.g. "Cranbrook" or "Biology calendar".</summary>
    string Name { get; }

    DeadlineSource Kind { get; }

    /// <summary>
    /// The due dates the last <see cref="FetchDeadlinesAsync"/> covered. A deadline from this source in that range
    /// that wasn't returned was removed at the source (for example a teacher deleted it), so it's removed here too.
    /// </summary>
    DateRange Covers { get; }

    /// <summary>
    /// Returns the source's deadlines. <see cref="Deadline.Id"/> is ignored (FocusBar assigns its own) and
    /// <see cref="Deadline.ExternalId"/> must be the source's own ID for the item.
    /// Throws if the source can't be reached; the caller keeps the last good data.
    /// </summary>
    Task<IReadOnlyList<Deadline>> FetchDeadlinesAsync(CancellationToken ct);

    /// <summary>The class schedule for the coming week. May return empty.</summary>
    Task<IReadOnlyList<ClassPeriod>> FetchScheduleAsync(CancellationToken ct);
}
