using System.Net;
using System.Text.Json;
using FocusBar.Core.Models;
using FocusBar.Core.Sources;
using FocusBar.Core.Sources.School;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Tests;

public sealed class SchoolApiTests : IDisposable
{
    private const string Host = "cranbrook.myschoolapp.com";
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4");
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 7, 30, 0, TimeSpan.FromHours(-4)); // Monday morning
    private static readonly Func<TimeSpan, CancellationToken, Task> NoDelay = (_, _) => Task.CompletedTask;

    private static readonly long FridayLateTicks = new DateTime(2026, 10, 30, 23, 59, 0).Ticks;

    private const string ContextJson = """
        { "UserInfo": { "UserId": 4242, "FirstName": "Suhaas", "LastName": "M" },
          "CurrentSchoolYear": { "SchoolYearLabel": "2026 - 2027" } }
        """;

    private const string SectionsJson = """
        [ { "sectionid": 111, "sectionTitle": "Biology - 3 (B)" },
          { "SectionId": "222", "GroupName": "Algebra II - 1 (A)" },
          { "sectionid": 333 } ]
        """;

    private static readonly string AssignmentsJson = $$"""
        [
          { "assignment_index_id": 9001, "section_id": 111, "groupname": "Biology - 3 (B)",
            "short_description": "Lab report: <b>Enzyme</b> activity &amp; rates",
            "long_description": "<p>Use the template.</p><p>Include  graphs</p>",
            "assignment_type": "Lab", "date_due": "10/30/2026 11:59 PM", "date_dueTicks": {{FridayLateTicks}},
            "assignment_status": -1 },
          { "assignment_index_id": "9002", "section_id": 222, "short_description": "Unit 4 test",
            "assignment_type": "Test", "date_due": "11/2/2026 9:00 AM", "assignment_status": 4 },
          { "assignment_index_id": 9003, "section_id": 999, "short_description": "",
            "assignment_type": "Reading", "date_due": "10/27/2026", "assignment_status": "1" },
          { "assignment_index_id": 9004, "short_description": "No due date" },
          { "short_description": "No id", "date_due": "10/30/2026" },
          { "assignment_index_id": 9005, "short_description": "Overdue worksheet", "date_due": "10/23/2026 3:00 PM", "assignment_status": 2 }
        ]
        """;

    private static readonly string ScheduleJson = $$"""
        [ { "Block": "A", "SectionTitle": "Biology - 3 (B)", "MyDayStartTime": "8:00 AM", "MyDayEndTime": "8:50 AM", "RoomNumber": "204" },
          { "Block": "B", "CourseTitle": "Algebra II", "StartTime": "10/26/2026 8:55 AM", "EndTime": "10/26/2026 9:45 AM" },
          { "Block": "Lunch", "StartTime": "/Date({{new DateTimeOffset(2026, 10, 26, 11, 40, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()}})/",
            "EndTime": "/Date({{new DateTimeOffset(2026, 10, 26, 12, 20, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()}})/" },
          { "Block": "Bad", "SectionTitle": "Backwards", "MyDayStartTime": "9:00 AM", "MyDayEndTime": "8:00 AM" },
          { "SectionTitle": "No times" } ]
        """;

    private readonly TempAppData _temp = new();

    public void Dispose() => _temp.Dispose();

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    /// <summary>A fake school site that answers each endpoint from the API guide.</summary>
    private static FakeHttpHandler FakeSite(Func<string, HttpResponseMessage?>? overrides = null) => new(request =>
    {
        var path = request.RequestUri!.AbsolutePath;
        if (overrides?.Invoke(path) is { } custom) return custom;
        return path switch
        {
            "/api/webapp/context/" => FakeHttpHandler.Json(ContextJson),
            "/api/datadirect/ParentStudentUserAcademicGroupsGet/4242/" => FakeHttpHandler.Json(SectionsJson),
            "/api/DataDirect/AssignmentCenterAssignments/" => FakeHttpHandler.Json(AssignmentsJson),
            "/api/schedule/MyDayCalendarStudentList/" => FakeHttpHandler.Json(ScheduleJson),
            _ => FakeHttpHandler.Status(HttpStatusCode.NotFound),
        };
    });

    private static SchoolApiSource Source(FakeHttpHandler handler, SchoolApiConfig? config = null) =>
        new(config ?? SchoolApiConfig.Default, new MySchoolAppClient(Host, "t=abc; AuthSvcToken=xyz", handler, NoDelay), () => Now, Eastern);

    // ----- Parsing -----

    [Fact]
    public void Parses_who_is_signed_in()
    {
        var context = SchoolApiParser.ParseContext(Json(ContextJson));
        Assert.Equal(4242, context.UserId);
        Assert.Equal("Suhaas M", context.DisplayName);
        Assert.Equal("2026 - 2027", context.SchoolYearLabel);
    }

    [Fact]
    public void Context_without_a_user_means_signed_out()
    {
        Assert.Throws<SchoolSignInException>(() => SchoolApiParser.ParseContext(Json("""{ "UserInfo": {} }""")));
    }

    [Fact]
    public void Parses_section_names_by_id()
    {
        var sections = SchoolApiParser.ParseSections(Json(SectionsJson));
        Assert.Equal("Biology - 3 (B)", sections["111"]);
        Assert.Equal("Algebra II - 1 (A)", sections["222"]);
        Assert.False(sections.ContainsKey("333"));
    }

    [Fact]
    public void Parses_assignments()
    {
        var sections = SchoolApiParser.ParseSections(Json(SectionsJson));
        var deadlines = SchoolApiParser.ParseAssignments(Json(AssignmentsJson), sections, Eastern);

        Assert.Equal(["9001", "9002", "9003", "9005"], deadlines.Select(d => d.ExternalId));
        Assert.All(deadlines, d => Assert.Equal(DeadlineSource.SchoolApi, d.Source));

        var lab = deadlines[0];
        Assert.Equal("Lab report: Enzyme activity & rates", lab.Title);
        Assert.Equal("Biology - 3 (B)", lab.ClassName);
        Assert.Equal("Lab\n\nUse the template.\nInclude graphs", lab.Description);
        Assert.Equal(new DateTimeOffset(2026, 10, 30, 23, 59, 0, TimeSpan.FromHours(-4)), lab.DueAt);
        Assert.False(lab.IsComplete);
    }

    [Fact]
    public void Ticks_are_read_as_the_wall_clock_time_the_portal_shows()
    {
        var lab = SchoolApiParser.ParseAssignments(Json(AssignmentsJson), new Dictionary<string, string>(), Eastern)[0];
        Assert.Equal(new DateTime(2026, 10, 30, 23, 59, 0), lab.DueAt.DateTime);
    }

    [Fact]
    public void Class_name_falls_back_to_the_section_list_and_title_to_the_type()
    {
        var sections = SchoolApiParser.ParseSections(Json(SectionsJson));
        var deadlines = SchoolApiParser.ParseAssignments(Json(AssignmentsJson), sections, Eastern);

        Assert.Equal("Algebra II - 1 (A)", deadlines[1].ClassName);
        Assert.Equal("Reading", deadlines[2].Title);
        Assert.Null(deadlines[2].ClassName);
    }

    [Fact]
    public void Completed_and_graded_count_as_done_but_overdue_does_not()
    {
        var deadlines = SchoolApiParser.ParseAssignments(Json(AssignmentsJson), new Dictionary<string, string>(), Eastern);
        Assert.Equal([false, true, true, false], deadlines.Select(d => d.IsComplete));
    }

    [Fact]
    public void Due_text_without_a_time_is_midnight_at_the_start_of_the_day()
    {
        var reading = SchoolApiParser.ParseAssignments(Json(AssignmentsJson), new Dictionary<string, string>(), Eastern)[2];
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.FromHours(-4)), reading.DueAt);
    }

    [Fact]
    public void A_list_wrapped_in_an_object_still_parses()
    {
        var wrapped = Json($$"""{ "Assignments": {{AssignmentsJson}} }""");
        Assert.Equal(4, SchoolApiParser.ParseAssignments(wrapped, new Dictionary<string, string>(), Eastern).Count);
    }

    [Fact]
    public void Parses_a_days_schedule_in_several_time_formats()
    {
        var monday = new DateOnly(2026, 10, 26);
        var periods = SchoolApiParser.ParseSchedule(Json(ScheduleJson), monday);

        Assert.Equal(
            [
                new ClassPeriod("Biology - 3 (B)", DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(8, 50), "204"),
                new ClassPeriod("Algebra II", DayOfWeek.Monday, new TimeOnly(8, 55), new TimeOnly(9, 45), null),
                new ClassPeriod("Lunch", DayOfWeek.Monday, new TimeOnly(11, 40), new TimeOnly(12, 20), null),
            ],
            periods);
    }

    [Theory]
    [InlineData("<p>Read <i>pages</i> 4&ndash;6</p><br/>Answer all", "Read pages 4–6\n\nAnswer all")]
    [InlineData("Plain &amp; simple", "Plain & simple")]
    [InlineData("   ", null)]
    public void Strips_html(string html, string? expected)
    {
        Assert.Equal(expected, SchoolApiParser.HtmlToText(html));
    }

    // ----- HTTP client -----

    [Fact]
    public async Task Sends_the_session_cookie_and_the_ajax_header()
    {
        var handler = FakeSite();
        using var client = new MySchoolAppClient(Host, "cookie: t=abc; AuthSvcToken=xyz", handler, NoDelay);

        await client.GetJsonAsync("/api/webapp/context/", null, CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://cranbrook.myschoolapp.com/api/webapp/context/", request.Url);
        Assert.Equal("t=abc; AuthSvcToken=xyz", request.Headers["Cookie"]);
        Assert.Equal("XMLHttpRequest", request.Headers["X-Requested-With"]);
    }

    [Fact]
    public async Task Keeps_cookies_the_site_rotates_and_drops_deleted_ones()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json("{}",
            "t=NEW; path=/; secure; HttpOnly",
            "old=gone; expires=Thu, 01 Jan 1970 00:00:00 GMT",
            "extra=1; Max-Age=3600"));
        using var client = new MySchoolAppClient(Host, "t=abc; old=1", handler, NoDelay);

        await client.GetJsonAsync("/api/webapp/context/", null, CancellationToken.None);

        Assert.True(client.CookiesChanged);
        Assert.Equal(MySchoolAppClient.ParseCookieHeader("t=NEW; extra=1"), MySchoolAppClient.ParseCookieHeader(client.CookieHeader));
        client.MarkCookiesSaved();
        Assert.False(client.CookiesChanged);
    }

    [Fact]
    public async Task Retries_a_403_once_then_succeeds()
    {
        var calls = 0;
        var handler = new FakeHttpHandler(_ => ++calls == 1 ? FakeHttpHandler.Status(HttpStatusCode.Forbidden) : FakeHttpHandler.Json(ContextJson));
        using var client = new MySchoolAppClient(Host, "t=abc", handler, NoDelay);

        await client.GetJsonAsync("/api/webapp/context/", null, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 1)]
    [InlineData(HttpStatusCode.Forbidden, 2)]
    public async Task Not_signed_in_is_reported_as_a_sign_in_problem(HttpStatusCode status, int expectedRequests)
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Status(status));
        using var client = new MySchoolAppClient(Host, "t=abc", handler, NoDelay);

        await Assert.ThrowsAsync<SchoolSignInException>(() => client.GetJsonAsync("/api/webapp/context/", null, CancellationToken.None));
        Assert.Equal(expectedRequests, handler.Requests.Count);
    }

    [Fact]
    public async Task Getting_the_sign_in_page_instead_of_data_means_signed_out()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Text("<!DOCTYPE html><html>Sign in</html>", "text/html"));
        using var client = new MySchoolAppClient(Host, "t=abc", handler, NoDelay);

        await Assert.ThrowsAsync<SchoolSignInException>(() => client.GetJsonAsync("/api/webapp/context/", null, CancellationToken.None));
    }

    [Fact]
    public async Task Server_errors_are_reported_without_retrying()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Status(HttpStatusCode.InternalServerError));
        using var client = new MySchoolAppClient(Host, "t=abc", handler, NoDelay);

        var error = await Assert.ThrowsAsync<SchoolApiException>(() => client.GetJsonAsync("/x", null, CancellationToken.None));
        Assert.Equal(HttpStatusCode.InternalServerError, error.Status);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void Parses_cookie_headers_copied_from_the_browser()
    {
        var cookies = MySchoolAppClient.ParseCookieHeader("cookie: t=ab=c==; AuthSvcToken=xyz;\r\n __RequestVerificationToken_OnSuite=q; junk; =novalue");
        Assert.Equal(new Dictionary<string, string>
        {
            ["t"] = "ab=c==",
            ["AuthSvcToken"] = "xyz",
            ["__RequestVerificationToken_OnSuite"] = "q",
        }, cookies);
    }

    [Fact]
    public void Builds_query_strings_with_escaping()
    {
        Assert.Equal("api/x/?dateStart=10%2F19%2F2026&schoolYearLabel=2026%20-%202027",
            MySchoolAppClient.BuildUrl("/api/x/", new Dictionary<string, string> { ["dateStart"] = "10/19/2026", ["schoolYearLabel"] = "2026 - 2027" }));
    }

    // ----- Source -----

    [Fact]
    public async Task Fetches_assignments_with_the_parameters_the_site_expects()
    {
        var handler = FakeSite();
        var source = Source(handler);

        var deadlines = await source.FetchDeadlinesAsync(CancellationToken.None);

        Assert.Equal(4, deadlines.Count);
        Assert.Equal("Suhaas M", source.Context!.DisplayName);

        var groups = handler.Requests.Single(r => r.Path.Contains("AcademicGroups"));
        Assert.Equal("2026 - 2027", groups.Query("schoolYearLabel"));
        Assert.Equal("2", groups.Query("personaId"));

        var assignments = handler.Requests.Single(r => r.Path.Contains("AssignmentCenter"));
        Assert.Equal("10/19/2026", assignments.Query("dateStart"));   // 7 days back
        Assert.Equal("12/25/2026", assignments.Query("dateEnd"));     // 60 days ahead
        Assert.Equal("4242", assignments.Query("userId"));
        Assert.Equal("2", assignments.Query("persona"));
        Assert.Equal("json", assignments.Query("format"));
        Assert.Equal("1", assignments.Query("filter"));

        Assert.Equal(new DateTime(2026, 10, 19), source.Covers.From.Date);
        Assert.Equal(new DateTime(2026, 12, 25), source.Covers.To.Date);
    }

    [Fact]
    public async Task Class_names_are_fetched_once_a_day_and_are_optional()
    {
        var handler = FakeSite(path => path.Contains("AcademicGroups") ? FakeHttpHandler.Status(HttpStatusCode.InternalServerError) : null);
        var source = Source(handler);

        var deadlines = await source.FetchDeadlinesAsync(CancellationToken.None);
        await source.FetchDeadlinesAsync(CancellationToken.None);

        Assert.Equal(4, deadlines.Count); // assignments still synced without class names
        Assert.Single(handler.Requests, r => r.Path.Contains("AcademicGroups"));
        Assert.Single(handler.Requests, r => r.Path.Contains("context"));
    }

    [Fact]
    public async Task Expired_sign_in_stops_the_sync()
    {
        var handler = FakeSite(path => path.Contains("AssignmentCenter") ? FakeHttpHandler.Status(HttpStatusCode.Unauthorized) : null);
        await Assert.ThrowsAsync<SchoolSignInException>(() => Source(handler).FetchDeadlinesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Fetches_a_week_of_schedule_one_day_at_a_time()
    {
        var handler = FakeSite();
        var schedule = await Source(handler).FetchScheduleAsync(CancellationToken.None);

        var days = handler.Requests.Where(r => r.Path.Contains("MyDayCalendar")).Select(r => r.Query("scheduleDate")).ToList();
        Assert.Equal(["10/26/2026", "10/27/2026", "10/28/2026", "10/29/2026", "10/30/2026", "10/31/2026", "11/1/2026"], days);
        Assert.All(handler.Requests.Where(r => r.Path.Contains("MyDayCalendar")), r => Assert.Equal("4242", r.Query("viewerId")));
        Assert.Equal(7 * 3, schedule.Count);
        Assert.Equal(7, schedule.Select(p => p.Day).Distinct().Count());
    }

    [Fact]
    public async Task One_bad_schedule_day_does_not_lose_the_week()
    {
        var calls = 0;
        var handler = FakeSite(path => path.Contains("MyDayCalendar") && ++calls == 3 ? FakeHttpHandler.Status(HttpStatusCode.InternalServerError) : null);
        var schedule = await Source(handler).FetchScheduleAsync(CancellationToken.None);
        Assert.Equal(6 * 3, schedule.Count);
    }

    [Fact]
    public async Task Uses_endpoint_paths_from_the_config_file()
    {
        var handler = FakeSite(path => path == "/custom/context" ? FakeHttpHandler.Json(ContextJson) : null);
        var config = SchoolApiConfig.Default with { Endpoints = new SchoolApiEndpoints(Context: "/custom/context") };

        var context = await Source(handler, config).ConnectAsync(CancellationToken.None);

        Assert.Equal(4242, context.UserId);
    }

    // ----- Demo data -----

    [Fact]
    public async Task Demo_source_has_realistic_assignments_and_a_weekday_schedule()
    {
        var demo = new MockSchoolApiSource(() => Now, Eastern);

        var deadlines = await demo.FetchDeadlinesAsync(CancellationToken.None);
        var schedule = await demo.FetchScheduleAsync(CancellationToken.None);

        Assert.Equal(8, deadlines.Count);
        Assert.Contains(deadlines, d => d.IsComplete);
        Assert.Contains(deadlines, d => d.DueAt < Now && !d.IsComplete);
        Assert.All(deadlines, d => Assert.StartsWith("demo-20261026-", d.ExternalId));
        Assert.Equal(5 * 7, schedule.Count);
        Assert.DoesNotContain(schedule, p => p.Day is DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.Equal(MockSchoolApiSource.SourceKey, demo.Key);
    }

    // ----- Config and saved sign-in -----

    [Theory]
    [InlineData("cranbrook.myschoolapp.com", true)]
    [InlineData("my-school.myschoolapp.com", true)]
    [InlineData("https://cranbrook.myschoolapp.com", false)]
    [InlineData("cranbrook", false)]
    [InlineData("bad host.com", false)]
    [InlineData("", false)]
    public void Validates_hosts(string host, bool valid)
    {
        Assert.Equal(valid, SchoolApiConfig.IsValidHost(host));
    }

    [Theory]
    [InlineData("https://Cranbrook.myschoolapp.com/app/#login", "cranbrook.myschoolapp.com")]
    [InlineData(" cranbrook.myschoolapp.com/ ", "cranbrook.myschoolapp.com")]
    public void Cleans_pasted_hosts(string input, string expected)
    {
        Assert.Equal(expected, SchoolApiConfig.CleanHost(input));
    }

    [Fact]
    public void Config_store_fills_in_and_repairs_values()
    {
        var store = new SchoolApiConfigStore(new JsonFileStore(_temp.Paths));
        Assert.Equal(SchoolMode.Off, store.Load().Mode);
        Assert.Equal(SchoolApiConfig.DefaultHost, store.Load().Host);

        Directory.CreateDirectory(_temp.Paths.Root);
        File.WriteAllText(_temp.Paths.File(SchoolApiConfig.FileName),
            """{ "Mode": "Live", "Host": "not a host", "PersonaId": 9, "DaysAhead": 9999, "Endpoints": { "Context": "" } }""");
        var repaired = store.Load();

        Assert.Equal(SchoolMode.Live, repaired.Mode);
        Assert.Equal(SchoolApiConfig.DefaultHost, repaired.Host);
        Assert.Equal(2, repaired.PersonaId);
        Assert.Equal(180, repaired.DaysAhead);
        Assert.Equal(7, repaired.ScheduleDays);
        Assert.Equal("/api/webapp/context/", repaired.Paths.Context);
        Assert.Equal("/api/DataDirect/AssignmentCenterAssignments/", repaired.Paths.Assignments);
    }

    [Fact]
    public void Saved_sign_in_is_encrypted_and_round_trips()
    {
        var store = new SchoolSessionStore(new JsonFileStore(_temp.Paths), new FakeProtector());
        var session = new SchoolSession(Host, "t=SECRET-TOKEN; AuthSvcToken=xyz", "Suhaas M", Now);

        store.Save(session);

        Assert.DoesNotContain("SECRET-TOKEN", File.ReadAllText(_temp.Paths.File(SchoolSessionStore.FileName)));
        Assert.Equal(session, store.Load());

        store.Clear();
        Assert.Null(store.Load());
    }

    [Fact]
    public void Sign_in_from_another_user_or_pc_reads_as_signed_out()
    {
        new SchoolSessionStore(new JsonFileStore(_temp.Paths), new FakeProtector())
            .Save(new SchoolSession(Host, "t=1", null, Now));
        Assert.Null(new SchoolSessionStore(new JsonFileStore(_temp.Paths), new FakeProtector(fail: true)).Load());
    }
}
