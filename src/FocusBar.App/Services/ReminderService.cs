using System.Windows.Threading;
using FocusBar.App.Ticker;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Models;
using FocusBar.Core.Reminders;
using FocusBar.Core.Settings;
using Microsoft.Toolkit.Uwp.Notifications;

namespace FocusBar.App.Services;

/// <summary>
/// Shows deadline reminders as Windows notifications. Checks every 30 seconds (and whenever deadlines change),
/// asks <see cref="ReminderPlanner"/> what's due, and remembers what it showed in <c>reminders.json</c>.
/// Notification buttons: Snooze 15 min and Mark complete. Clicking the notification opens the deadline list.
/// </summary>
internal sealed class ReminderService : IDisposable
{
    public static readonly TimeSpan SnoozeLength = TimeSpan.FromMinutes(15);

    private const string ToastGroup = "deadlines";

    private readonly DeadlineService _deadlines;
    private readonly SettingsService _settings;
    private readonly ReminderStateStore _store;
    private readonly ErrorLog _log;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;
    private readonly ReminderState _state;
    private readonly HashSet<Guid> _showing = []; // deadlines with a notification shown since FocusBar started
    private bool _disposed;

    public ReminderService(DeadlineService deadlines, SettingsService settings, ReminderStateStore store, ErrorLog log, Dispatcher dispatcher)
    {
        _deadlines = deadlines;
        _settings = settings;
        _store = store;
        _log = log;
        _dispatcher = dispatcher;
        _state = store.Load();

        try
        {
            // Button clicks arrive here, on a background thread, even if FocusBar was started by the click.
            ToastNotificationManagerCompat.OnActivated += OnToastActivated;
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not set up Windows notifications; reminder buttons won't work.");
        }

        _timer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) => Check(), dispatcher);
        _timer.Start();
        _deadlines.Changed += OnDeadlinesChanged;
        Check();
    }

    /// <summary>Opens the deadline list (set by the app, since the list window needs the deadline service).</summary>
    public Action? OpenList { get; set; }

    public void Snooze(Guid deadlineId)
    {
        _state.Snooze(deadlineId, DateTimeOffset.Now + SnoozeLength);
        RemoveToast(deadlineId);
        Save();
    }

    /// <summary>Shows a sample notification so the student can check that notifications work.</summary>
    public void ShowTest()
    {
        var sample = new Deadline(Guid.Empty, "Test reminder", "FocusBar", null, DateTimeOffset.Now.AddHours(1),
            DeadlineSource.Manual, null, false);
        Show(new Reminder(sample, TimeSpan.FromHours(1), false), withButtons: false);
    }

    private void OnDeadlinesChanged(object? sender, EventArgs e)
    {
        // Completed (or deleted) deadlines shouldn't leave their reminders sitting in the notification center.
        foreach (var id in _showing.ToList())
        {
            if (_deadlines.Find(id) is { IsComplete: false }) continue;
            RemoveToast(id);
        }
        Check();
    }

    private void Check()
    {
        if (_disposed) return;

        try
        {
            var before = Fingerprint();
            var all = _deadlines.ForReminders();
            var due = ReminderPlanner.Due(all, _settings.Current.Reminders, _state, DateTimeOffset.Now);
            _state.Prune(_deadlines.AllIds());

            foreach (var reminder in due) Show(reminder, withButtons: true);
            if (Fingerprint() != before) Save();
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Reminder check failed.");
        }
    }

    private void Show(Reminder reminder, bool withButtons)
    {
        var deadline = reminder.Deadline;
        var id = deadline.Id.ToString("N");
        var details = string.IsNullOrWhiteSpace(deadline.ClassName)
            ? $"Due {DeadlineDisplay.ShortDue(deadline)}"
            : $"{deadline.ClassName} · due {DeadlineDisplay.ShortDue(deadline)}";
        if (reminder.IsCatchUp) details += " (FocusBar was off when this reminder was due)";

        try
        {
            var toast = new ToastContentBuilder()
                .AddArgument("action", "open")
                .AddArgument("id", id)
                .AddText(ReminderPlanner.Headline(deadline, DateTimeOffset.Now))
                .AddText(details)
                .AddAttributionText("FocusBar");

            if (withButtons)
            {
                toast.AddButton(new ToastButton()
                        .SetContent($"Snooze {(int)SnoozeLength.TotalMinutes} min")
                        .AddArgument("action", "snooze")
                        .AddArgument("id", id)
                        .SetBackgroundActivation())
                    .AddButton(new ToastButton()
                        .SetContent("Mark complete")
                        .AddArgument("action", "complete")
                        .AddArgument("id", id)
                        .SetBackgroundActivation());
            }

            // One notification per deadline: a newer reminder replaces the older one.
            toast.Show(t =>
            {
                t.Tag = id;
                t.Group = ToastGroup;
            });
            if (deadline.Id != Guid.Empty) _showing.Add(deadline.Id);
        }
        catch (Exception ex)
        {
            _log.Write(ex, $"Could not show a reminder for \"{deadline.Title}\". Windows notifications may be turned off.");
        }
    }

    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        ToastArguments args;
        try
        {
            args = ToastArguments.Parse(e.Argument);
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not read a notification click.");
            return;
        }

        if (!args.TryGetValue("action", out var action)) return;
        args.TryGetValue("id", out var idText);
        Guid.TryParseExact(idText, "N", out var id);

        _dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return;
            switch (action)
            {
                case "snooze" when id != Guid.Empty:
                    Snooze(id);
                    break;
                case "complete" when id != Guid.Empty:
                    _deadlines.SetComplete(id, true);
                    break;
                case "focus":
                case "lowmemory":
                    break; // Focus Mode's and the low-memory notice's own toasts: nothing to open
                default:
                    OpenList?.Invoke();
                    break;
            }
        });
    }

    private void RemoveToast(Guid deadlineId)
    {
        _showing.Remove(deadlineId);
        try
        {
            ToastNotificationManagerCompat.History.Remove(deadlineId.ToString("N"), ToastGroup);
        }
        catch
        {
            // Nothing to remove, or notifications unavailable.
        }
    }

    private int Fingerprint() => HashCode.Combine(_state.Fired.Count, _state.FirstSeen.Count, _state.Snoozed.Count);

    private void Save()
    {
        try
        {
            _store.Save(_state);
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not save reminders.json; a reminder might show again after a restart.");
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _deadlines.Changed -= OnDeadlinesChanged;
        try { ToastNotificationManagerCompat.OnActivated -= OnToastActivated; } catch { }
    }
}
