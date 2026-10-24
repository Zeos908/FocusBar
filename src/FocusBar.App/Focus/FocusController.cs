using System.Windows.Threading;
using FocusBar.App.Services;
using FocusBar.Core.Deadlines;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Focus;
using FocusBar.Core.Models;

namespace FocusBar.App.Focus;

internal enum WebsiteStatus
{
    /// <summary>No sites to block (none listed, Gentle mode, or Focus Mode is off).</summary>
    NotBlocking,

    /// <summary>Waiting for the permission prompt or the helper.</summary>
    Working,

    Blocked,

    /// <summary>The student said No to the permission prompt.</summary>
    Declined,

    Failed,
}

/// <summary>
/// Runs Focus Mode: decides when it's on (via <see cref="FocusPlanner"/>), closes or warns about blocked apps,
/// blocks websites through the elevated helper, shows the Focus screen, and cleans up when it ends.
/// It never resists being closed: exiting FocusBar ends Focus Mode and removes the website block.
/// </summary>
internal sealed class FocusController : IFocusScreenActions, IDisposable
{
    private static readonly TimeSpan EvaluateEvery = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ScanEvery = TimeSpan.FromSeconds(2);

    private readonly SettingsService _settings;
    private readonly DeadlineService _deadlines;
    private readonly FocusStateStore _store;
    private readonly ErrorLog _log;
    private readonly Func<string?> _screenImage;
    private readonly FocusState _state;
    private readonly DispatcherTimer _evaluateTimer;
    private readonly DispatcherTimer _scanTimer;
    private readonly HashSet<string> _allowed = new(StringComparer.OrdinalIgnoreCase); // apps let through for this session
    private readonly HashSet<int> _handled = [];                                         // processes already dealt with
    private readonly SemaphoreSlim _websiteLock = new(1, 1);

    private FocusScreen? _screen;
    private HashSet<string>? _requestedSites; // what the hosts file was last asked to block (null = nothing asked yet)
    private Task<WebsiteBlockResult>? _helperTask; // the latest helper run
    private bool _disposed;

    /// <param name="screenImage">The current theme's Focus screen image (a full path), if it has one.</param>
    public FocusController(SettingsService settings, DeadlineService deadlines, FocusStateStore store, Func<string?> screenImage,
        ErrorLog log, Dispatcher dispatcher)
    {
        _settings = settings;
        _deadlines = deadlines;
        _store = store;
        _screenImage = screenImage;
        _log = log;
        _state = store.Load();

        _evaluateTimer = new DispatcherTimer(EvaluateEvery, DispatcherPriority.Background, (_, _) => Evaluate(), dispatcher);
        _scanTimer = new DispatcherTimer(ScanEvery, DispatcherPriority.Background, (_, _) => Scan(), dispatcher);
        _scanTimer.Stop();

        _deadlines.Changed += OnInputsChanged;
        _settings.Changed += OnInputsChanged;
    }

    /// <summary>Raised when Focus Mode turns on or off, or its website status changes.</summary>
    public event EventHandler? Changed;

    public FocusSession? Active { get; private set; }
    public WebsiteStatus Websites { get; private set; }
    public FocusSettings Settings => _settings.Current.Focus;
    public bool CanEnd => Active is { } session && FocusPlanner.CanEndEarly(session, Settings);

    /// <summary>
    /// Call once at startup. Resumes a session that's still running, and removes a website block left behind
    /// by a crash if Focus Mode shouldn't be on.
    /// </summary>
    public void Start()
    {
        Evaluate();
        if (Active is null && WebsiteBlocker.HasBlock())
        {
            _log.Info("Found a Focus Mode website block from an earlier run; removing it.");
            _ = UpdateWebsitesAsync(force: true);
        }
    }

    /// <summary>"Focus for 25 / 50 / 90 min".</summary>
    public void StartManual(TimeSpan length)
    {
        var now = DateTimeOffset.Now;
        _state.Manual = new ManualSession(now, now + length);
        Save();
        Evaluate();
    }

    /// <summary>Ends the current session early, if the strictness level allows it.</summary>
    public void End()
    {
        if (Active is not { } session || !CanEnd) return;
        if (session.Reason == FocusReason.Manual) _state.Manual = null;
        else if (session.DismissKey is { } key) _state.Dismissed.Add(key);
        Save();
        Evaluate();
    }

    /// <summary>Tries website blocking again after the permission prompt was declined or failed.</summary>
    public void RetryWebsites() => _ = UpdateWebsitesAsync(force: true);

    /// <summary>
    /// On exit, after the taskbar is back: removes the website block, waiting for the student to answer Windows'
    /// permission prompt. If they say No, the next FocusBar start removes it (or run "FocusBar.Blocker.exe clear").
    /// </summary>
    public void RemoveWebsiteBlockForExit()
    {
        // Let a helper that's already running finish first, so it can't re-add the block after we remove it.
        try
        {
            _helperTask?.Wait(TimeSpan.FromSeconds(30));
        }
        catch (AggregateException)
        {
            // Logged by the helper call itself.
        }
        WebsiteBlocker.Clear(_log);
    }

    // ----- Deciding -----

    private void OnInputsChanged(object? sender, EventArgs e) => Evaluate();

    private void Evaluate()
    {
        if (_disposed) return;
        var now = DateTimeOffset.Now;

        if (_state.Manual is { } manual && manual.EndsAt <= now)
        {
            _state.Manual = null;
            Save();
        }
        PruneDismissed();

        var previous = Active;
        var next = FocusPlanner.Current(Settings, _deadlines.ForReminders(), _state.Manual, _state.Dismissed, now);
        Active = next;

        if (previous is null && next is not null) OnStarted(next);
        else if (previous is not null && next is null) OnEnded();

        _ = UpdateWebsitesAsync(force: false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnStarted(FocusSession session)
    {
        _allowed.Clear();
        _handled.Clear();
        _scanTimer.Start();
        Scan();

        var why = session.Deadline is { } d
            ? $"{d.Title} is due in {Countdown.Format(d.DueAt, DateTimeOffset.Now)}."
            : $"Focus session until {session.EndsAt.ToLocalTime():t}.";
        var sitesNote = Settings.BlockedSites.Count > 0 && BlockList.BlocksWebsites(Settings.Strictness)
            ? " Windows will ask for permission to block websites."
            : "";
        FocusNotifications.Show("Focus Mode is on", why + sitesNote, _log);
        _log.Info($"Focus Mode on ({session.Reason}) until {session.EndsAt:t}.");
    }

    private void OnEnded()
    {
        _scanTimer.Stop();
        _allowed.Clear();
        _handled.Clear();
        _screen?.Close();
        FocusNotifications.Show("Focus Mode is off", "Your apps and websites are unblocked.", _log);
        _log.Info("Focus Mode off.");
    }

    /// <summary>Forgets "ended early" markers for deadlines that are gone or past.</summary>
    private void PruneDismissed()
    {
        if (_state.Dismissed.Count == 0) return;
        var ids = _deadlines.AllIds();
        var removed = _state.Dismissed.RemoveWhere(key =>
            !Guid.TryParseExact(key.Split('|')[0], "N", out var id) || !ids.Contains(id) ||
            !long.TryParse(key.Split('|').ElementAtOrDefault(1), out var ticks) || ticks < DateTimeOffset.UtcNow.UtcTicks);
        if (removed > 0) Save();
    }

    // ----- Apps -----

    private void Scan()
    {
        if (_disposed || Active is not { } session) return;

        List<BlockedProcess> found;
        try
        {
            found = AppBlocker.FindBlocked(Settings, _allowed);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Write(ex, "Focus Mode couldn't list running apps.");
            return;
        }

        var fresh = found.Where(p => _handled.Add(p.Id)).ToList();
        if (fresh.Count == 0) return;

        var strictness = Settings.Strictness;
        if (strictness != Strictness.Gentle)
        {
            foreach (var process in fresh) _ = AppBlocker.CloseAsync(process.Id, _log);
        }

        // One screen for the first app; a launcher that starts several processes shouldn't flash several screens.
        ShowScreen(fresh[0], session, strictness);
    }

    private void ShowScreen(BlockedProcess app, FocusSession session, Strictness strictness)
    {
        if (_screen is null)
        {
            _screen = new FocusScreen(_log);
            _screen.Closed += (_, _) => _screen = null;
        }
        _screen.ShowFor(app, session, strictness, Settings.SoftOverrideDelay, this, _screenImage());
    }

    void IFocusScreenActions.AllowForSession(string appName)
    {
        _allowed.Add(appName);
        _log.Info($"Focus Mode: {appName} allowed for the rest of this session.");
    }

    void IFocusScreenActions.CloseApp(string appName)
    {
        foreach (var process in AppBlocker.FindBlocked(Settings, _allowed).Where(p => p.Name.Equals(appName, StringComparison.OrdinalIgnoreCase)))
        {
            _ = AppBlocker.CloseAsync(process.Id, _log);
        }
    }

    // ----- Websites -----

    /// <summary>
    /// Brings the hosts file in line with what should be blocked right now. Only runs the helper (and its permission
    /// prompt) when that changes, and doesn't re-prompt after a "No" until the student retries or the list changes.
    /// </summary>
    private async Task UpdateWebsitesAsync(bool force)
    {
        var wanted = Active is not null && BlockList.BlocksWebsites(Settings.Strictness)
            ? Settings.BlockedSites.ToHashSet(StringComparer.Ordinal)
            : [];

        if (!force && _requestedSites is not null && _requestedSites.SetEquals(wanted)) return;
        if (!force && _requestedSites is null && wanted.Count == 0) return; // nothing blocked, nothing asked: no prompt
        _requestedSites = wanted;

        await _websiteLock.WaitAsync();
        try
        {
            if (_disposed) return;
            Websites = wanted.Count == 0 ? WebsiteStatus.NotBlocking : WebsiteStatus.Working;
            Changed?.Invoke(this, EventArgs.Empty);

            _helperTask = WebsiteBlocker.ApplyAsync(wanted, _log);
            var result = await _helperTask;
            if (_disposed) return;

            Websites = result switch
            {
                WebsiteBlockResult.Done => wanted.Count == 0 ? WebsiteStatus.NotBlocking : WebsiteStatus.Blocked,
                WebsiteBlockResult.Declined => wanted.Count == 0 ? WebsiteStatus.NotBlocking : WebsiteStatus.Declined,
                _ => wanted.Count == 0 ? WebsiteStatus.NotBlocking : WebsiteStatus.Failed,
            };
            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _websiteLock.Release();
        }
    }

    private void Save()
    {
        try
        {
            _store.Save(_state);
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not save focus-state.json.");
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _evaluateTimer.Stop();
        _scanTimer.Stop();
        _screen?.Close();
        _deadlines.Changed -= OnInputsChanged;
        _settings.Changed -= OnInputsChanged;
    }
}
