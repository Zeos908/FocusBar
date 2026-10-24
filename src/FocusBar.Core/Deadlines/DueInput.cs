using System.Globalization;
using System.Text.RegularExpressions;

namespace FocusBar.Core.Deadlines;

/// <summary>
/// Understands the due date and time the way a student would type them in the add form:
/// "tomorrow", "fri", "10/31", "Oct 31", "3pm", "3:30 pm", "15:30", "noon", "midnight".
/// </summary>
public static partial class DueInput
{
    /// <summary>Default time for a new deadline: online assignments are commonly due at 11:59 PM.</summary>
    public static readonly TimeOnly DefaultTime = new(23, 59);

    /// <summary>
    /// Parses a due date relative to <paramref name="today"/>. A weekday name means the next one after today.
    /// A date typed without a year that has already passed this year means next year.
    /// </summary>
    public static bool TryParseDate(string? text, DateOnly today, CultureInfo culture, out DateOnly date)
    {
        date = default;
        var input = text?.Trim().TrimEnd('.').ToLowerInvariant() ?? "";
        if (input.Length == 0) return false;

        switch (input)
        {
            case "today" or "tod" or "tdy":
                date = today;
                return true;
            case "tomorrow" or "tmrw" or "tmr" or "tom":
                date = today.AddDays(1);
                return true;
        }

        if (TryParseWeekday(input, culture, out var weekday))
        {
            var ahead = ((int)weekday - (int)today.DayOfWeek + 7) % 7;
            date = today.AddDays(ahead == 0 ? 7 : ahead);
            return true;
        }

        if (!DateTime.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault, out var parsed))
        {
            return false;
        }

        // Text with only a time ("3pm") parses to year 1 because of NoCurrentDateDefault; that isn't a date.
        if (parsed.Year == 1) return false;

        date = DateOnly.FromDateTime(parsed);
        if (!HasYear(text!) && date < today) date = date.AddYears(1);
        return true;
    }

    /// <summary>Parses a time of day. "midnight" means 11:59 PM, the end of the due date, not the start of it.</summary>
    public static bool TryParseTime(string? text, CultureInfo culture, out TimeOnly time)
    {
        time = default;
        var input = text?.Trim().ToLowerInvariant() ?? "";
        if (input.Length == 0) return false;

        switch (input)
        {
            case "noon" or "midday":
                time = new TimeOnly(12, 0);
                return true;
            case "midnight" or "end of day" or "eod":
                time = DefaultTime;
                return true;
        }

        var match = SimpleTime().Match(input);
        if (match.Success)
        {
            var hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
            var minute = match.Groups["minute"].Success ? int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture) : 0;
            var meridiem = match.Groups["ampm"].Value;

            if (minute > 59) return false;
            if (meridiem.Length > 0)
            {
                if (hour is < 1 or > 12) return false;
                var pm = meridiem.StartsWith('p');
                hour = hour % 12 + (pm ? 12 : 0);
            }
            else if (hour > 23)
            {
                return false;
            }

            time = new TimeOnly(hour, minute);
            return true;
        }

        // Fall back to the region's own format (for example other languages' AM/PM words).
        if (DateTime.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault, out var parsed)
            && parsed.Date == DateTime.MinValue.Date)
        {
            time = TimeOnly.FromDateTime(parsed);
            return true;
        }

        return false;
    }

    /// <summary>Combines a date and time into a moment in the computer's time zone.</summary>
    public static DateTimeOffset Combine(DateOnly date, TimeOnly time, TimeZoneInfo? zone = null)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        zone ??= TimeZoneInfo.Local;
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private static bool TryParseWeekday(string input, CultureInfo culture, out DayOfWeek day)
    {
        var format = culture.DateTimeFormat;
        foreach (DayOfWeek candidate in Enum.GetValues<DayOfWeek>())
        {
            var full = format.GetDayName(candidate).ToLower(culture);
            var english = candidate.ToString().ToLowerInvariant();
            // "fri", "frid", "friday" all work. Prefixes that fit two days ("t", "s") are rejected rather than guessed.
            if (input.Length >= 2 && (full.StartsWith(input, StringComparison.Ordinal) || english.StartsWith(input, StringComparison.Ordinal)))
            {
                day = candidate;
                return !IsAmbiguousWeekday(input, format, culture, candidate);
            }
        }

        day = default;
        return false;
    }

    private static bool IsAmbiguousWeekday(string input, DateTimeFormatInfo format, CultureInfo culture, DayOfWeek found) =>
        Enum.GetValues<DayOfWeek>().Any(other => other != found &&
            (format.GetDayName(other).ToLower(culture).StartsWith(input, StringComparison.Ordinal) ||
             other.ToString().ToLowerInvariant().StartsWith(input, StringComparison.Ordinal)));

    /// <summary>True if the user typed a year: a 4-digit number, or three separate numbers like 10/31/26.</summary>
    private static bool HasYear(string text) =>
        FourDigits().IsMatch(text) || Numbers().Count(text) >= 3;

    [GeneratedRegex(@"^(?<hour>\d{1,2})(?::?(?<minute>\d{2}))?\s*(?<ampm>[ap]\.?(m\.?)?)?$")]
    private static partial Regex SimpleTime();

    [GeneratedRegex(@"\d{4}")]
    private static partial Regex FourDigits();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Numbers();
}
