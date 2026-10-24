using System.Globalization;
using FocusBar.Core.Models;

namespace FocusBar.Core.Deadlines;

/// <param name="Period">The class.</param>
/// <param name="IsNow">True if it's happening now; false if it's the next one.</param>
/// <param name="StartsOn">The date of the class (today for a current class).</param>
public record ClassAtTime(ClassPeriod Period, bool IsNow, DateOnly StartsOn);

/// <summary>Finds the class happening now, or the next one, from the weekly schedule.</summary>
public static class ScheduleLookup
{
    /// <summary>
    /// The class in progress at <paramref name="now"/>, otherwise the next one within the coming week.
    /// Null when there's no schedule.
    /// </summary>
    public static ClassAtTime? CurrentOrNext(IReadOnlyCollection<ClassPeriod> schedule, DateTime now)
    {
        if (schedule.Count == 0) return null;

        var today = DateOnly.FromDateTime(now);
        var time = TimeOnly.FromDateTime(now);

        var current = schedule
            .Where(p => p.Day == now.DayOfWeek && p.Start <= time && time < p.End)
            .OrderBy(p => p.Start)
            .FirstOrDefault();
        if (current is not null) return new ClassAtTime(current, true, today);

        for (var offset = 0; offset <= 7; offset++)
        {
            var date = today.AddDays(offset);
            var next = schedule
                .Where(p => p.Day == date.DayOfWeek && (offset > 0 || p.Start > time))
                .OrderBy(p => p.Start)
                .FirstOrDefault();
            if (next is not null) return new ClassAtTime(next, false, date);
        }

        return null;
    }

    /// <summary>"Now: Biology · until 10:15", "Next: Math · 10:20", or "Mon: Math · 8:00 AM".</summary>
    public static string Describe(ClassAtTime value, DateOnly today, CultureInfo culture)
    {
        var period = value.Period;
        var room = string.IsNullOrWhiteSpace(period.Room) ? "" : $" ({period.Room.Trim()})";

        if (value.IsNow)
        {
            return $"Now: {period.ClassName}{room} · until {period.End.ToString("t", culture)}";
        }

        var when = period.Start.ToString("t", culture);
        if (value.StartsOn == today) return $"Next: {period.ClassName}{room} · {when}";
        if (value.StartsOn == today.AddDays(1)) return $"Tomorrow: {period.ClassName}{room} · {when}";
        return $"{culture.DateTimeFormat.GetAbbreviatedDayName(period.Day)}: {period.ClassName}{room} · {when}";
    }
}
