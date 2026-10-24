using System.Text.RegularExpressions;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Sources.School;

public enum SchoolMode
{
    /// <summary>No school sync.</summary>
    Off,

    /// <summary>Realistic made-up assignments and schedule, for trying FocusBar (or judging it) without a school account.</summary>
    Demo,

    /// <summary>Sync from the school website with the saved sign-in.</summary>
    Live,
}

/// <summary>
/// Paths on the school site. These are the routes the myschoolapp.com website itself calls
/// (from the API guide the student provided). Kept in <c>school-api.json</c> so they can be fixed without a rebuild.
/// </summary>
public record SchoolApiEndpoints(
    string Context = "/api/webapp/context/",
    string Assignments = "/api/DataDirect/AssignmentCenterAssignments/",
    string Schedule = "/api/schedule/MyDayCalendarStudentList/",
    string AcademicGroups = "/api/datadirect/ParentStudentUserAcademicGroupsGet/{userId}/");

/// <summary>
/// Settings for the school website sync, saved to <c>school-api.json</c> (no secrets: the sign-in is stored
/// separately, encrypted).
/// </summary>
/// <param name="Host">The school's Blackbaud onCampus site.</param>
/// <param name="PersonaId">2 = student, 3 = parent.</param>
/// <param name="DaysBack">How far back to fetch assignments (to show recently overdue work).</param>
/// <param name="DaysAhead">How far ahead to fetch assignments.</param>
/// <param name="ScheduleDays">How many days of class schedule to fetch.</param>
public partial record SchoolApiConfig(
    SchoolMode Mode = SchoolMode.Off,
    string Host = SchoolApiConfig.DefaultHost,
    int PersonaId = 2,
    int DaysBack = 7,
    int DaysAhead = 60,
    int ScheduleDays = 7,
    SchoolApiEndpoints? Endpoints = null)
{
    public const string FileName = "school-api.json";
    public const string DefaultHost = "cranbrook.myschoolapp.com";

    public static SchoolApiConfig Default => new();

    public SchoolApiEndpoints Paths => Endpoints ?? new SchoolApiEndpoints();

    /// <summary>
    /// True for a plain host name such as "cranbrook.myschoolapp.com" (no "https://", path, or spaces).
    /// </summary>
    public static bool IsValidHost(string? host) =>
        !string.IsNullOrWhiteSpace(host) && HostPattern().IsMatch(host) && host.Contains('.');

    /// <summary>Accepts what a student might paste ("https://cranbrook.myschoolapp.com/app/") and keeps the host.</summary>
    public static string CleanHost(string input)
    {
        var text = input.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Host.Length > 0) return uri.Host.ToLowerInvariant();
        return text.TrimEnd('/').ToLowerInvariant();
    }

    [GeneratedRegex(@"^[a-zA-Z0-9]([a-zA-Z0-9-]*[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9-]*[a-zA-Z0-9])?)+$")]
    private static partial Regex HostPattern();
}

/// <summary>Loads and saves <see cref="SchoolApiConfig"/>, repairing missing or invalid values.</summary>
public sealed class SchoolApiConfigStore
{
    private readonly JsonFileStore _store;

    public SchoolApiConfigStore(JsonFileStore store)
    {
        _store = store;
    }

    public SchoolApiConfig Load()
    {
        var c = _store.Load<SchoolApiConfig?>(SchoolApiConfig.FileName, null) ?? SchoolApiConfig.Default;
        var defaults = new SchoolApiEndpoints();
        var paths = c.Endpoints ?? defaults;
        return c with
        {
            Host = SchoolApiConfig.IsValidHost(c.Host) ? c.Host : SchoolApiConfig.DefaultHost,
            PersonaId = c.PersonaId is 2 or 3 ? c.PersonaId : 2,
            DaysBack = Math.Clamp(c.DaysBack, 0, 60),
            DaysAhead = Math.Clamp(c.DaysAhead == 0 ? 60 : c.DaysAhead, 1, 180),
            ScheduleDays = Math.Clamp(c.ScheduleDays == 0 ? 7 : c.ScheduleDays, 1, 7),
            Endpoints = new SchoolApiEndpoints(
                Context: Pick(paths.Context, defaults.Context),
                Assignments: Pick(paths.Assignments, defaults.Assignments),
                Schedule: Pick(paths.Schedule, defaults.Schedule),
                AcademicGroups: Pick(paths.AcademicGroups, defaults.AcademicGroups)),
        };
    }

    public void Save(SchoolApiConfig config) => _store.Save(SchoolApiConfig.FileName, config);

    private static string Pick(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) || !value.StartsWith('/') ? fallback : value;
}
