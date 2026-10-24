using System.Globalization;
using FocusBar.Core.Models;

namespace FocusBar.Core.Sources.School;

/// <summary>
/// Deadlines and the class schedule from the school website (Blackbaud onCampus / myschoolapp.com),
/// using the endpoints from the student's API guide:
/// <list type="bullet">
/// <item><c>/api/webapp/context/</c>: who's signed in (user ID, school year).</item>
/// <item><c>/api/DataDirect/AssignmentCenterAssignments/</c>: assignments in a date range.</item>
/// <item><c>/api/datadirect/ParentStudentUserAcademicGroupsGet/{userId}/</c>: class names for section IDs.</item>
/// <item><c>/api/schedule/MyDayCalendarStudentList/</c>: one day's class blocks.</item>
/// </list>
/// </summary>
public sealed class SchoolApiSource : IDeadlineSource
{
    public const string SourceKey = "school";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly SchoolApiConfig _config;
    private readonly MySchoolAppClient _client;
    private readonly Func<DateTimeOffset> _now;
    private readonly TimeZoneInfo _zone;
    private Dictionary<string, string>? _sections;
    private DateOnly _sectionsDate;

    public SchoolApiSource(SchoolApiConfig config, MySchoolAppClient client, Func<DateTimeOffset>? now = null, TimeZoneInfo? zone = null)
    {
        _config = config;
        _client = client;
        _now = now ?? (() => DateTimeOffset.Now);
        _zone = zone ?? TimeZoneInfo.Local;
    }

    public string Key => SourceKey;
    public string Name => $"School ({_config.Host})";
    public DeadlineSource Kind => DeadlineSource.SchoolApi;
    public DateRange Covers { get; private set; }

    /// <summary>Who's signed in; set after the first successful request.</summary>
    public SchoolContext? Context { get; private set; }

    /// <summary>Checks the sign-in works and returns who it belongs to.</summary>
    public async Task<SchoolContext> ConnectAsync(CancellationToken ct)
    {
        var json = await _client.GetJsonAsync(_config.Paths.Context, null, ct).ConfigureAwait(false);
        Context = SchoolApiParser.ParseContext(json);
        return Context;
    }

    public async Task<IReadOnlyList<Deadline>> FetchDeadlinesAsync(CancellationToken ct)
    {
        var context = Context ?? await ConnectAsync(ct).ConfigureAwait(false);
        var sections = await SectionsAsync(context, ct).ConfigureAwait(false);

        var now = _now();
        var window = DateRange.Around(now, _config.DaysBack, _config.DaysAhead);
        var json = await _client.GetJsonAsync(_config.Paths.Assignments, new Dictionary<string, string>
        {
            ["format"] = "json",
            ["filter"] = "1",
            ["dateStart"] = SiteDate(DateOnly.FromDateTime(window.From.Date)),
            ["dateEnd"] = SiteDate(DateOnly.FromDateTime(window.To.Date)),
            ["persona"] = _config.PersonaId.ToString(Invariant),
            ["userId"] = context.UserId.ToString(Invariant),
        }, ct).ConfigureAwait(false);

        Covers = window;
        return SchoolApiParser.ParseAssignments(json, sections, _zone);
    }

    /// <summary>
    /// The coming week of classes, one request per day. Schools with rotating schedules (Day A/B) work too,
    /// because each date's real schedule is fetched; FocusBar refreshes it at least daily.
    /// </summary>
    public async Task<IReadOnlyList<ClassPeriod>> FetchScheduleAsync(CancellationToken ct)
    {
        var context = Context ?? await ConnectAsync(ct).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(_now().LocalDateTime);
        var periods = new List<ClassPeriod>();

        for (var i = 0; i < _config.ScheduleDays; i++)
        {
            var date = today.AddDays(i);
            try
            {
                var json = await _client.GetJsonAsync(_config.Paths.Schedule, new Dictionary<string, string>
                {
                    ["scheduleDate"] = SiteDate(date),
                    ["personaId"] = _config.PersonaId.ToString(Invariant),
                    ["viewerId"] = context.UserId.ToString(Invariant),
                }, ct).ConfigureAwait(false);
                periods.AddRange(SchoolApiParser.ParseSchedule(json, date));
            }
            catch (SchoolApiException ex) when (ex is not SchoolSignInException)
            {
                // One bad day (for example a holiday the site errors on) shouldn't lose the rest of the week.
            }
        }

        return periods;
    }

    /// <summary>Class names, fetched at most once a day. Optional: if it fails, assignments still sync.</summary>
    private async Task<IReadOnlyDictionary<string, string>> SectionsAsync(SchoolContext context, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_now().LocalDateTime);
        if (_sections is not null && _sectionsDate == today) return _sections;

        try
        {
            var path = _config.Paths.AcademicGroups.Replace("{userId}", context.UserId.ToString(Invariant), StringComparison.Ordinal);
            var query = new Dictionary<string, string> { ["personaId"] = _config.PersonaId.ToString(Invariant) };
            if (!string.IsNullOrWhiteSpace(context.SchoolYearLabel)) query["schoolYearLabel"] = context.SchoolYearLabel;

            var json = await _client.GetJsonAsync(path, query, ct).ConfigureAwait(false);
            _sections = SchoolApiParser.ParseSections(json);
            _sectionsDate = today;
        }
        catch (SchoolApiException ex) when (ex is not SchoolSignInException)
        {
            // Try again tomorrow rather than on every sync; assignments usually carry their class name anyway.
            _sections ??= [];
            _sectionsDate = today;
        }

        return _sections;
    }

    /// <summary>The site's date format: M/D/YYYY.</summary>
    private static string SiteDate(DateOnly date) => date.ToString("M/d/yyyy", Invariant);
}
