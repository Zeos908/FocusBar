namespace FocusBar.Core.Deadlines;

/// <summary>
/// Short "time left" text for the ticker: "2d 4h", "3h 12m", "45m", "&lt;1m", or "2h overdue".
/// Two units at most, so it stays short on a thin bar.
/// </summary>
public static class Countdown
{
    public static string Format(DateTimeOffset dueAt, DateTimeOffset now)
    {
        var remaining = dueAt - now;
        if (remaining > TimeSpan.Zero) return FormatSpan(remaining);
        return remaining > -TimeSpan.FromMinutes(1) ? "due now" : FormatSpan(-remaining) + " overdue";
    }

    /// <summary>Longer wording for tooltips and details: "in 2 days, 4 hours" / "2 hours ago".</summary>
    public static string Describe(DateTimeOffset dueAt, DateTimeOffset now)
    {
        var remaining = dueAt - now;
        if (remaining > TimeSpan.Zero) return "in " + DescribeSpan(remaining);
        return remaining > -TimeSpan.FromMinutes(1) ? "now" : DescribeSpan(-remaining) + " ago";
    }

    private static string FormatSpan(TimeSpan span)
    {
        // Round down: "1h 59m" left should never read as "2h".
        var days = (int)span.TotalDays;
        if (days >= 1) return span.Hours > 0 ? $"{days}d {span.Hours}h" : $"{days}d";

        var hours = (int)span.TotalHours;
        if (hours >= 1) return span.Minutes > 0 ? $"{hours}h {span.Minutes}m" : $"{hours}h";

        var minutes = (int)span.TotalMinutes;
        return minutes >= 1 ? $"{minutes}m" : "<1m";
    }

    private static string DescribeSpan(TimeSpan span)
    {
        var days = (int)span.TotalDays;
        if (days >= 1) return span.Hours > 0 ? $"{Plural(days, "day")}, {Plural(span.Hours, "hour")}" : Plural(days, "day");

        var hours = (int)span.TotalHours;
        if (hours >= 1) return span.Minutes > 0 ? $"{Plural(hours, "hour")}, {Plural(span.Minutes, "minute")}" : Plural(hours, "hour");

        var minutes = (int)span.TotalMinutes;
        return minutes >= 1 ? Plural(minutes, "minute") : "less than a minute";
    }

    private static string Plural(int count, string unit) => count == 1 ? $"1 {unit}" : $"{count} {unit}s";
}
