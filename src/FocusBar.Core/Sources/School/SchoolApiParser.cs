using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;

namespace FocusBar.Core.Sources.School;

/// <summary>Who's signed in, from <c>/api/webapp/context/</c>.</summary>
public record SchoolContext(long UserId, string? FirstName, string? LastName, string? SchoolYearLabel)
{
    public string DisplayName => string.Join(' ', new[] { FirstName, LastName }.Where(n => !string.IsNullOrWhiteSpace(n)));
}

/// <summary>
/// Turns the school website's JSON into FocusBar deadlines and class periods. Field names come from the API guide;
/// because the guide notes some vary by school and role, every field is looked up by several likely names,
/// ignoring case, and anything unexpected is skipped instead of failing the whole sync.
/// </summary>
public static partial class SchoolApiParser
{
    /// <summary>Assignment statuses (from the API guide) that mean the work is done: completed and graded.</summary>
    private static readonly HashSet<int> DoneStatuses = [1, 4];

    private static readonly string[] DueTextFormats =
    [
        "M/d/yyyy h:mm:ss tt", "M/d/yyyy h:mm tt", "M/d/yyyy H:mm:ss", "M/d/yyyy H:mm", "M/d/yyyy",
        "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm", "yyyy-MM-dd",
    ];

    public static SchoolContext ParseContext(JsonElement root)
    {
        var user = Field(root, "UserInfo") ?? root;
        var id = Long(Field(user, "UserId", "Id", "user_id"));
        if (id is null or <= 0)
        {
            throw new SchoolSignInException("The school website didn't say who's signed in. Your sign-in may have expired.");
        }

        var year = Field(root, "CurrentSchoolYear") is { } y ? Text(Field(y, "SchoolYearLabel", "Label")) : null;
        return new SchoolContext(id.Value, Text(Field(user, "FirstName", "first_name")), Text(Field(user, "LastName", "last_name")), year);
    }

    /// <summary>Section ID → class name, from the student's academic groups (or the Group Finder).</summary>
    public static Dictionary<string, string> ParseSections(JsonElement root)
    {
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in Items(root))
        {
            var id = Text(Field(item, "sectionid", "section_id", "leadsectionid", "lead_pk", "current_pk", "Id"));
            var name = Clean(Text(Field(item, "sectionTitle", "groupname", "group_name", "coursetitle", "course_title", "sectionidentifier")));
            if (id is not null && name is not null) sections.TryAdd(id, name);
        }
        return sections;
    }

    /// <summary>Assignment Center items → deadlines (with the school's assignment index ID as the external ID).</summary>
    public static IReadOnlyList<Deadline> ParseAssignments(JsonElement root, IReadOnlyDictionary<string, string> sections, TimeZoneInfo zone)
    {
        var results = new List<Deadline>();
        foreach (var item in Items(root))
        {
            var id = Text(Field(item, "assignment_index_id", "AssignmentIndexId", "assignment_id", "AssignmentId"));
            var title = Clean(HtmlToText(Text(Field(item, "short_description", "ShortDescription", "title"))))
                ?? Clean(Text(Field(item, "assignment_type", "AssignmentType")));
            var due = Due(item, zone);
            if (id is null || title is null || due is null) continue;

            var sectionId = Text(Field(item, "section_id", "SectionId", "sectionid"));
            var className = Clean(Text(Field(item, "groupname", "GroupName", "section_name", "sectionTitle")))
                ?? (sectionId is not null && sections.TryGetValue(sectionId, out var name) ? name : null);

            var type = Clean(Text(Field(item, "assignment_type", "AssignmentType")));
            var details = Clean(HtmlToText(Text(Field(item, "long_description", "LongDescription"))));
            var description = (type, details) switch
            {
                (null, null) => null,
                (null, _) => details,
                (_, null) => type,
                _ => $"{type}\n\n{details}",
            };

            var status = Long(Field(item, "assignment_status", "AssignmentStatus"));
            var done = status is { } s && DoneStatuses.Contains((int)s);

            results.Add(new Deadline(Guid.Empty, title, className, description, due.Value, DeadlineSource.SchoolApi, id, done));
        }
        return results;
    }

    /// <summary>One day's class blocks → class periods for that day of the week.</summary>
    public static IReadOnlyList<ClassPeriod> ParseSchedule(JsonElement root, DateOnly date)
    {
        var periods = new List<ClassPeriod>();
        foreach (var item in Items(root))
        {
            var name = Clean(Text(Field(item, "SectionTitle", "CourseTitle", "GroupName", "Title", "Block")));
            var start = TimeOf(Field(item, "MyDayStartTime", "StartTime", "start_time", "Start"));
            var end = TimeOf(Field(item, "MyDayEndTime", "EndTime", "end_time", "End"));
            if (name is null || start is null || end is null || end <= start) continue;

            var room = Clean(Text(Field(item, "RoomNumber", "Room", "room", "BuildingName")));
            var period = new ClassPeriod(name, date.DayOfWeek, start.Value, end.Value, room);
            if (!periods.Contains(period)) periods.Add(period);
        }
        return [.. periods.OrderBy(p => p.Start)];
    }

    /// <summary>
    /// The due time. <c>date_dueTicks</c> is .NET ticks of the wall-clock time the portal shows (per the API guide),
    /// so it's read as a local time on this computer, not converted from UTC.
    /// </summary>
    private static DateTimeOffset? Due(JsonElement item, TimeZoneInfo zone)
    {
        if (Long(Field(item, "date_dueTicks", "DateDueTicks")) is { } ticks && ticks > 0 && ticks < DateTime.MaxValue.Ticks)
        {
            var wall = new DateTime(ticks, DateTimeKind.Unspecified);
            return DueInput.Combine(DateOnly.FromDateTime(wall), TimeOnly.FromDateTime(wall), zone);
        }

        var text = Text(Field(item, "date_due", "DateDue", "due_date"));
        if (text is not null && DateTime.TryParseExact(text.Trim(), DueTextFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            return DueInput.Combine(DateOnly.FromDateTime(parsed), TimeOnly.FromDateTime(parsed), zone);
        }

        return null;
    }

    /// <summary>A time of day from "8:00 AM", "08:00", "10/26/2026 8:00 AM", or "/Date(1793001600000)/".</summary>
    internal static TimeOnly? TimeOf(JsonElement? value)
    {
        var text = Text(value)?.Trim();
        if (string.IsNullOrEmpty(text)) return null;

        var msDate = MsDate().Match(text);
        if (msDate.Success && long.TryParse(msDate.Groups[1].Value, out var ms))
        {
            // Like the ticks fields, these encode the wall-clock time, so read them as UTC.
            return TimeOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime);
        }

        var us = CultureInfo.GetCultureInfo("en-US");
        if (TimeOnly.TryParse(text, us, DateTimeStyles.AllowWhiteSpaces, out var time)) return time;
        if (DateTime.TryParse(text, us, DateTimeStyles.AllowWhiteSpaces, out var dateTime)) return TimeOnly.FromDateTime(dateTime);
        return null;
    }

    /// <summary>Assignment titles and descriptions can contain HTML; keep just the text.</summary>
    public static string? HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var text = LineBreakTags().Replace(html, "\n");
        text = Tags().Replace(text, "");
        text = WebUtility.HtmlDecode(text);
        var lines = text.Split('\n').Select(l => Spaces().Replace(l, " ").Trim());
        return string.Join('\n', lines).Trim('\n', ' ');
    }

    /// <summary>The list in a response: the response itself, or the first list inside it.</summary>
    private static IEnumerable<JsonElement> Items(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object);
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array) return Items(property.Value);
            }
        }
        return [];
    }

    /// <summary>The first of <paramref name="names"/> present on the object, ignoring case.</summary>
    private static JsonElement? Field(JsonElement obj, params string[] names)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
        {
            foreach (var property in obj.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind != JsonValueKind.Null)
                {
                    return property.Value;
                }
            }
        }
        return null;
    }

    private static string? Text(JsonElement? value) => value?.ValueKind switch
    {
        JsonValueKind.String => value.Value.GetString(),
        JsonValueKind.Number => value.Value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null,
    };

    private static long? Long(JsonElement? value) => value?.ValueKind switch
    {
        JsonValueKind.Number when value.Value.TryGetInt64(out var n) => n,
        JsonValueKind.String when long.TryParse(value.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
        _ => null,
    };

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    [GeneratedRegex(@"<\s*(br|/p|/div|/li|/h[1-6])\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakTags();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"/Date\((-?\d+)[^)]*\)/")]
    private static partial Regex MsDate();
}
