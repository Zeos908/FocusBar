namespace FocusBar.Core.Status;

/// <summary>The dates shown in the clock's calendar flyout: always 6 rows of 7 days, like the Windows calendar.</summary>
public static class MonthGrid
{
    public const int Days = 42;

    /// <summary>
    /// The 42 dates to show for <paramref name="month"/>, starting on <paramref name="firstDayOfWeek"/>
    /// (Sunday in the US; comes from the user's region settings). Includes the tail of the previous month
    /// and the start of the next one.
    /// </summary>
    public static IReadOnlyList<DateOnly> Build(int year, int month, DayOfWeek firstDayOfWeek)
    {
        var first = new DateOnly(year, month, 1);
        var leading = ((int)first.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        var start = first.AddDays(-leading);
        return [.. Enumerable.Range(0, Days).Select(start.AddDays)];
    }
}
