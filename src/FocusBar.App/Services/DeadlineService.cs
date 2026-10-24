using FocusBar.Core.Deadlines;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Models;
using FocusBar.Core.Sources;

namespace FocusBar.App.Services;

/// <summary>
/// The app's single copy of the deadlines. Every change is saved to <c>deadlines.json</c> right away and
/// announced through <see cref="Changed"/> so the ticker and the deadline list redraw.
/// </summary>
internal sealed class DeadlineService
{
    private readonly DeadlineList _list;
    private readonly DeadlineStore _store;
    private readonly ScheduleStore _scheduleStore;
    private readonly ErrorLog _log;

    public DeadlineService(DeadlineStore store, ScheduleStore schedule, ErrorLog log)
    {
        _store = store;
        _scheduleStore = schedule;
        _log = log;
        _list = store.Load();
        Schedule = schedule.Load();
    }

    /// <summary>Raised on the UI thread after any change.</summary>
    public event EventHandler? Changed;

    /// <summary>The coming week's classes, from the school sync (empty when there's no school connection).</summary>
    public IReadOnlyList<ClassPeriod> Schedule { get; private set; }

    public IReadOnlyList<Deadline> Upcoming() => _list.Upcoming();

    public IReadOnlyList<Deadline> Completed() => _list.Completed();

    public Deadline? Find(Guid id) => _list.Find(id);

    /// <summary>
    /// The deadlines reminders are about: each one once, with duplicates from different sources folded together,
    /// so the same assignment doesn't remind twice. (Completed ones are skipped by the planner.)
    /// </summary>
    public IReadOnlyList<Deadline> ForReminders() => _list.Merged();

    public IReadOnlySet<Guid> AllIds() => _list.All.Select(d => d.Id).ToHashSet();

    public IReadOnlyList<string> RecentClassNames(int max) => _list.RecentClassNames(max);

    public Deadline Add(string title, string? className, string? description, DateTimeOffset dueAt)
    {
        var added = _list.AddManual(title, className, description, dueAt);
        SaveAndNotify();
        return added;
    }

    public void Edit(Guid id, string title, string? className, string? description, DateTimeOffset dueAt)
    {
        if (_list.Edit(id, title, className, description, dueAt)) SaveAndNotify();
    }

    public void SetComplete(Guid id, bool complete)
    {
        if (_list.SetComplete(id, complete)) SaveAndNotify();
    }

    public void Delete(Guid id)
    {
        if (_list.Remove(id)) SaveAndNotify();
    }

    /// <summary>Brings in a source's latest deadlines (see <see cref="DeadlineList.ApplySync"/>).</summary>
    public SyncChanges ApplySync(IDeadlineSource source, IReadOnlyList<Deadline> fetched)
    {
        var changes = _list.ApplySync(source.Key, source.Kind, fetched, source.Covers);
        if (changes.Any) SaveAndNotify();
        return changes;
    }

    /// <summary>Removes everything a source added (when it's disconnected or removed).</summary>
    public void RemoveSource(string sourceKey)
    {
        if (_list.RemoveSource(sourceKey) > 0) SaveAndNotify();
    }

    public void SetSchedule(IReadOnlyList<ClassPeriod> schedule)
    {
        if (Schedule.SequenceEqual(schedule)) return;
        Schedule = schedule;
        try
        {
            _scheduleStore.Save(schedule);
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not save schedule.json.");
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SaveAndNotify()
    {
        try
        {
            _store.Save(_list);
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not save deadlines.json; recent deadline changes may be lost when FocusBar closes.");
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
