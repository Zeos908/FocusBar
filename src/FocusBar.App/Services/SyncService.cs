using System.IO;
using System.Net.Http;
using System.Windows.Threading;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Sources;
using FocusBar.Core.Sources.School;

namespace FocusBar.App.Services;

/// <param name="Key">The source's key.</param>
/// <param name="Name">Shown to the student.</param>
/// <param name="LastSuccess">When it last synced successfully.</param>
/// <param name="Error">What went wrong on the last try, or null if it worked.</param>
/// <param name="NeedsSignIn">True when the school sign-in has expired.</param>
internal sealed record SourceStatus(string Key, string Name, DateTimeOffset? LastSuccess, string? Error, bool NeedsSignIn);

/// <summary>
/// Keeps deadlines in step with the school website and the student's calendars: syncs a few seconds after
/// startup, every 30 minutes, and on "Sync now". A source that fails keeps its last good deadlines, shows a
/// warning icon on the bar, and never crashes the app. Also manages connecting and disconnecting sources.
/// </summary>
internal sealed class SyncService : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan FirstSyncDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ScheduleRefresh = TimeSpan.FromHours(6);
    private static readonly TimeSpan SourceTimeout = TimeSpan.FromSeconds(90);

    private readonly DeadlineService _deadlines;
    private readonly SchoolApiConfigStore _configStore;
    private readonly SchoolSessionStore _sessionStore;
    private readonly CalendarFeedStore _feedStore;
    private readonly ErrorLog _log;
    private readonly DispatcherTimer _timer;
    private readonly HttpClient _calendarHttp = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly Dictionary<string, SourceStatus> _status = [];

    private IDeadlineSource? _school;
    private MySchoolAppClient? _schoolClient;
    private DateTimeOffset _lastScheduleFetch = DateTimeOffset.MinValue;
    private Task? _running;
    private bool _disposed;

    public SyncService(DeadlineService deadlines, SchoolApiConfigStore configStore, SchoolSessionStore sessionStore,
        CalendarFeedStore feedStore, ErrorLog log, Dispatcher dispatcher)
    {
        _deadlines = deadlines;
        _configStore = configStore;
        _sessionStore = sessionStore;
        _feedStore = feedStore;
        _log = log;

        SchoolConfig = configStore.Load();
        Session = sessionStore.Load();
        Feeds = feedStore.Load();
        _calendarHttp.DefaultRequestHeaders.UserAgent.ParseAdd("FocusBar/0.1");

        _timer = new DispatcherTimer(FirstSyncDelay, DispatcherPriority.Background, async (_, _) =>
        {
            _timer!.Interval = Interval;
            await SyncNowAsync(); // never throws: RunAsync catches everything
        }, dispatcher);
        _timer.Start();
    }

    /// <summary>Raised on the UI thread when a sync starts or finishes, or a source is added or removed.</summary>
    public event EventHandler? StatusChanged;

    public SchoolApiConfig SchoolConfig { get; private set; }
    public SchoolSession? Session { get; private set; }
    public IReadOnlyList<CalendarFeed> Feeds { get; private set; }
    public bool IsSyncing => _running is { IsCompleted: false };
    public DateTimeOffset? LastSync { get; private set; }

    public IReadOnlyCollection<SourceStatus> Statuses => _status.Values;

    public SourceStatus? StatusOf(string key) => _status.GetValueOrDefault(key);

    public bool HasProblems => _status.Values.Any(s => s.Error is not null);

    /// <summary>One line per failing source, for the warning icon's tooltip.</summary>
    public string ProblemSummary => string.Join("\n", _status.Values.Where(s => s.Error is not null).Select(s => $"{s.Name}: {s.Error}"));

    /// <summary>Syncs every source now. If a sync is already running, waits for it instead of starting another.</summary>
    public Task SyncNowAsync()
    {
        if (_disposed) return Task.CompletedTask;
        if (_running is { IsCompleted: false }) return _running;
        _running = RunAsync();
        return _running;
    }

    // ----- School website -----

    /// <summary>
    /// Checks the session cookies from the sign-in window (or pasted by the student), and if they work, saves them
    /// (encrypted), switches school sync on, and syncs. Throws <see cref="SchoolSignInException"/> if they don't work.
    /// </summary>
    public async Task<SchoolContext> ConnectSchoolAsync(string host, string cookieHeader, CancellationToken ct)
    {
        var config = SchoolConfig with { Host = host };
        var client = new MySchoolAppClient(host, cookieHeader);
        if (!client.HasCookies)
        {
            client.Dispose();
            throw new SchoolSignInException("That doesn't look like a cookie header. It should look like \"t=...; AuthSvcToken=...\".");
        }

        var source = new SchoolApiSource(config, client);
        SchoolContext context;
        try
        {
            context = await source.ConnectAsync(ct);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        Session = new SchoolSession(host, client.CookieHeader, context.DisplayName, DateTimeOffset.Now);
        _sessionStore.Save(Session);
        client.MarkCookiesSaved();
        SetConfig(config with { Mode = SchoolMode.Live });

        ReplaceSchoolSource(source, client);
        _deadlines.RemoveSource(MockSchoolApiSource.SourceKey);
        _status.Remove(MockSchoolApiSource.SourceKey);
        _lastScheduleFetch = DateTimeOffset.MinValue;
        _log.Info($"Connected to {host}.");

        _ = SyncNowAsync();
        return context;
    }

    /// <summary>Uses made-up school data instead of a real account.</summary>
    public void UseDemoData()
    {
        SetConfig(SchoolConfig with { Mode = SchoolMode.Demo });
        ReplaceSchoolSource(null, null);
        _deadlines.RemoveSource(SchoolApiSource.SourceKey);
        _status.Remove(SchoolApiSource.SourceKey);
        _lastScheduleFetch = DateTimeOffset.MinValue;
        _ = SyncNowAsync();
    }

    /// <summary>Stops school sync and deletes the saved sign-in and the school's deadlines and schedule.</summary>
    public void DisconnectSchool()
    {
        SetConfig(SchoolConfig with { Mode = SchoolMode.Off });
        _sessionStore.Clear();
        Session = null;
        ReplaceSchoolSource(null, null);
        _deadlines.RemoveSource(SchoolApiSource.SourceKey);
        _deadlines.RemoveSource(MockSchoolApiSource.SourceKey);
        _deadlines.SetSchedule([]);
        _status.Remove(SchoolApiSource.SourceKey);
        _status.Remove(MockSchoolApiSource.SourceKey);
        _log.Info("Disconnected the school website.");
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    // ----- Calendars -----

    /// <summary>Adds a calendar and syncs it. Returns false if the same link or file is already added.</summary>
    public bool AddCalendar(CalendarFeed feed)
    {
        if (Feeds.Any(f => f.Key == feed.Key)) return false;
        Feeds = [.. Feeds, feed];
        _feedStore.Save(Feeds);
        _ = SyncNowAsync();
        return true;
    }

    public void RemoveCalendar(string key)
    {
        Feeds = [.. Feeds.Where(f => f.Key != key)];
        _feedStore.Save(Feeds);
        _deadlines.RemoveSource(key);
        _status.Remove(key);
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    // ----- Syncing -----

    private async Task RunAsync()
    {
        StatusChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            foreach (var source in CurrentSources())
            {
                if (_disposed) return;
                await SyncOneAsync(source);
            }
            LastSync = DateTimeOffset.Now;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Write(ex, "Sync failed.");
        }
        finally
        {
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task SyncOneAsync(IDeadlineSource source)
    {
        using var timeout = new CancellationTokenSource(SourceTimeout);
        var previous = _status.GetValueOrDefault(source.Key);
        try
        {
            var deadlines = await source.FetchDeadlinesAsync(timeout.Token);
            if (_disposed || !IsCurrent(source)) return;
            var changes = _deadlines.ApplySync(source, deadlines);
            if (changes.Any) _log.Info($"Synced {source.Name}: {changes.Added} added, {changes.Updated} updated, {changes.Removed} removed.");

            if (source.Kind == Core.Models.DeadlineSource.SchoolApi && ScheduleIsStale())
            {
                var schedule = await source.FetchScheduleAsync(timeout.Token);
                if (_disposed || !IsCurrent(source)) return;
                _deadlines.SetSchedule(schedule);
                _lastScheduleFetch = DateTimeOffset.Now;
            }

            _status[source.Key] = new SourceStatus(source.Key, source.Name, DateTimeOffset.Now, null, false);
        }
        catch (Exception) when (_disposed || !IsCurrent(source))
        {
            // Disconnected or removed while syncing; its result no longer matters.
        }
        catch (SchoolSignInException ex)
        {
            if (previous?.NeedsSignIn != true) _log.Warning($"{source.Name}: {ex.Message}");
            _status[source.Key] = new SourceStatus(source.Key, source.Name, previous?.LastSuccess,
                "Your school sign-in has expired. Sign in again in Settings → Deadline sources.", true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Write(ex, $"Sync failed for {source.Name}.");
            _status[source.Key] = new SourceStatus(source.Key, source.Name, previous?.LastSuccess, Friendly(ex), false);
        }
        finally
        {
            SaveRotatedCookies();
        }
    }

    /// <summary>The sources to sync right now: the school (real or demo) and each calendar.</summary>
    private List<IDeadlineSource> CurrentSources()
    {
        var sources = new List<IDeadlineSource>();

        switch (SchoolConfig.Mode)
        {
            case SchoolMode.Demo:
                _school ??= new MockSchoolApiSource();
                sources.Add(_school);
                break;
            case SchoolMode.Live when Session is not null:
                if (_school is not SchoolApiSource)
                {
                    var client = new MySchoolAppClient(Session.Host, Session.CookieHeader);
                    ReplaceSchoolSource(new SchoolApiSource(SchoolConfig with { Host = Session.Host }, client), client);
                }
                sources.Add(_school!);
                break;
            case SchoolMode.Live:
                _status[SchoolApiSource.SourceKey] = new SourceStatus(SchoolApiSource.SourceKey, $"School ({SchoolConfig.Host})",
                    null, "Not signed in. Sign in in Settings → Deadline sources.", true);
                break;
        }

        sources.AddRange(Feeds.Select(feed => new IcsCalendarSource(feed, _calendarHttp)));
        return sources;
    }

    /// <summary>False if the source was replaced or removed while it was syncing (don't apply stale results).</summary>
    private bool IsCurrent(IDeadlineSource source) =>
        ReferenceEquals(source, _school) || Feeds.Any(f => f.Key == source.Key);

    private bool ScheduleIsStale() =>
        DateTimeOffset.Now - _lastScheduleFetch > ScheduleRefresh || _lastScheduleFetch.Date != DateTimeOffset.Now.Date;

    /// <summary>The school rotates its session cookie; save the newest one so the sign-in keeps working.</summary>
    private void SaveRotatedCookies()
    {
        if (_schoolClient is not { CookiesChanged: true } client || Session is null) return;
        try
        {
            Session = Session with { CookieHeader = client.CookieHeader, SavedAt = DateTimeOffset.Now };
            _sessionStore.Save(Session);
            client.MarkCookiesSaved();
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not save the updated school sign-in.");
        }
    }

    private void ReplaceSchoolSource(IDeadlineSource? source, MySchoolAppClient? client)
    {
        _schoolClient?.Dispose();
        _schoolClient = client;
        _school = source;
    }

    private void SetConfig(SchoolApiConfig config)
    {
        SchoolConfig = config;
        try
        {
            _configStore.Save(config);
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not save school-api.json.");
        }
    }

    private string Friendly(Exception ex) => ex switch
    {
        SchoolApiException school => school.Message,
        HttpRequestException => "Couldn't connect. Check your internet connection.",
        TaskCanceledException or OperationCanceledException => "It took too long to answer. FocusBar will try again later.",
        FileNotFoundException or DirectoryNotFoundException => "The calendar file can't be found. Was it moved or deleted?",
        UnauthorizedAccessException => "FocusBar isn't allowed to read that file.",
        _ when ex.GetType().Namespace?.StartsWith("Ical", StringComparison.Ordinal) == true || ex is FormatException
            => "This doesn't look like a calendar (.ics) file.",
        _ => "Something went wrong. Details are in the log.",
    };

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _schoolClient?.Dispose();
        _calendarHttp.Dispose();
    }
}
