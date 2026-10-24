using System.Security.Cryptography;
using System.Text;
using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace FocusBar.Core.Sources;

/// <summary>A calendar the student added: a local .ics file or a subscription link (https:// or webcal://).</summary>
/// <param name="Name">What the student called it, e.g. "Biology calendar".</param>
/// <param name="Location">A file path or URL.</param>
/// <param name="ClassName">Optional class to show for its events when they don't name one.</param>
public record CalendarFeed(string Name, string Location, string? ClassName = null)
{
    public bool IsUrl => Location.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || Location.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || Location.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase);

    /// <summary>A stable key from the location, so re-adding the same calendar doesn't duplicate it.</summary>
    public string Key
    {
        get
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Location.Trim().ToLowerInvariant()));
            return "calendar-" + Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
        }
    }
}

/// <summary>
/// Reads deadlines from an iCalendar (.ics) file or link, using Ical.Net. Each event (VEVENT) is a deadline due at
/// its start time; an all-day event is due at 11:59 PM that day. Tasks (VTODO) use their due date. Repeating events
/// are skipped: they're usually classes or practices, not deadlines.
/// </summary>
public sealed class IcsCalendarSource : IDeadlineSource
{
    public const int DaysBack = 7;
    public const int DaysAhead = 60;

    private readonly CalendarFeed _feed;
    private readonly HttpClient? _http;
    private readonly Func<DateTimeOffset> _now;

    /// <param name="http">Used for links; may be null if the feed is a file.</param>
    public IcsCalendarSource(CalendarFeed feed, HttpClient? http, Func<DateTimeOffset>? now = null)
    {
        _feed = feed;
        _http = http;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public string Key => _feed.Key;
    public string Name => _feed.Name;
    public DeadlineSource Kind => DeadlineSource.Calendar;
    public DateRange Covers { get; private set; }

    public async Task<IReadOnlyList<Deadline>> FetchDeadlinesAsync(CancellationToken ct)
    {
        var text = await ReadAsync(ct).ConfigureAwait(false);
        Covers = DateRange.Around(_now(), DaysBack, DaysAhead);
        return Parse(text, _feed.ClassName, Covers, TimeZoneInfo.Local);
    }

    public Task<IReadOnlyList<ClassPeriod>> FetchScheduleAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ClassPeriod>>([]);

    private async Task<string> ReadAsync(CancellationToken ct)
    {
        if (!_feed.IsUrl) return await File.ReadAllTextAsync(_feed.Location, ct).ConfigureAwait(false);

        if (_http is null) throw new InvalidOperationException("No HTTP client for a calendar link.");
        var url = _feed.Location.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)
            ? "https://" + _feed.Location["webcal://".Length..]
            : _feed.Location;
        return await _http.GetStringAsync(url, ct).ConfigureAwait(false);
    }

    /// <summary>Turns .ics text into deadlines due within <paramref name="window"/>.</summary>
    public static IReadOnlyList<Deadline> Parse(string icsText, string? defaultClass, DateRange window, TimeZoneInfo zone)
    {
        var calendar = Calendar.Load(EnsureUids(icsText)) ?? throw new FormatException("This isn't a calendar (.ics) file.");
        var results = new List<Deadline>();

        foreach (var e in calendar.Events)
        {
            if (IsRepeating(e) || e.Status?.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) == true) continue;
            if (ToDue(e.DtStart, zone) is not { } due || !window.Contains(due)) continue;
            Add(results, e, due, isComplete: false, defaultClass);
        }

        foreach (var todo in calendar.Todos)
        {
            if (IsRepeating(todo) || todo.IsCancelled) continue;
            if (ToDue(todo.Due ?? todo.DtStart, zone) is not { } due || !window.Contains(due)) continue;
            var done = todo.Completed is not null || todo.Status?.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase) == true;
            Add(results, todo, due, done, defaultClass);
        }

        return results;
    }

    private static void Add(List<Deadline> results, RecurringComponent item, DateTimeOffset due, bool isComplete, string? defaultClass)
    {
        var title = item.Summary?.Trim();
        if (string.IsNullOrEmpty(title)) return;

        var uid = item.Uid?.Trim();
        if (string.IsNullOrEmpty(uid)) return;
        var className = item.Categories?.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim() ?? defaultClass;

        results.Add(new Deadline(Guid.Empty, title, className, item.Description?.Trim(), due,
            DeadlineSource.Calendar, uid, isComplete));
    }

    /// <summary>
    /// Gives each event or task without a UID (some exporters leave it out) one made from its contents.
    /// Otherwise Ical.Net invents a random one on every read, and the item would look new on every sync.
    /// </summary>
    internal static string EnsureUids(string icsText)
    {
        var lines = icsText.Replace("\r\n", "\n").Split('\n');
        var output = new List<string>(lines.Length + 8);
        var depth = 0;
        int? itemStart = null;
        var hasUid = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase))
            {
                depth++;
                var kind = line[6..].Trim();
                if (depth == 2 && (kind.Equals("VEVENT", StringComparison.OrdinalIgnoreCase) || kind.Equals("VTODO", StringComparison.OrdinalIgnoreCase)))
                {
                    itemStart = output.Count;
                    hasUid = false;
                }
            }
            else if (depth == 2 && itemStart is not null &&
                     (line.StartsWith("UID:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("UID;", StringComparison.OrdinalIgnoreCase)))
            {
                hasUid = true;
            }
            else if (line.StartsWith("END:", StringComparison.OrdinalIgnoreCase))
            {
                if (depth == 2 && itemStart is { } start)
                {
                    if (!hasUid)
                    {
                        var block = string.Join("\n", output.Skip(start));
                        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(block)), 0, 12).ToLowerInvariant();
                        output.Add($"UID:focusbar-{hash}");
                    }
                    itemStart = null;
                }
                depth = Math.Max(0, depth - 1);
            }
            output.Add(line);
        }

        return string.Join("\r\n", output);
    }

    private static bool IsRepeating(RecurringComponent item) =>
        item.RecurrenceRule is not null || item.RecurrenceDates?.GetAllDates().Any() == true;

    /// <summary>
    /// Converts an iCalendar date/time to a moment. All-day → 11:59 PM that day; "floating" (no time zone) →
    /// this computer's time; otherwise the time zone in the file.
    /// </summary>
    internal static DateTimeOffset? ToDue(CalDateTime? value, TimeZoneInfo zone)
    {
        if (value is null) return null;

        if (!value.HasTime) return DueInput.Combine(new DateOnly(value.Year, value.Month, value.Day), DueInput.DefaultTime, zone);

        if (!value.IsFloating)
        {
            try
            {
                return TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(value.AsUtc, DateTimeKind.Utc)), zone);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A time zone Ical.Net doesn't know: fall back to treating it as local time.
            }
        }

        var wall = value.Value;
        return DueInput.Combine(DateOnly.FromDateTime(wall), TimeOnly.FromDateTime(wall), zone);
    }
}
