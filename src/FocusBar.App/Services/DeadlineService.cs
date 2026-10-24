using FocusBar.Core.Deadlines;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Models;

namespace FocusBar.App.Services;

/// <summary>
/// The app's single copy of the deadlines. Every change is saved to <c>deadlines.json</c> right away and
/// announced through <see cref="Changed"/> so the ticker and the deadline list redraw.
/// </summary>
internal sealed class DeadlineService
{
    private readonly DeadlineList _list;
    private readonly DeadlineStore _store;
    private readonly ErrorLog _log;

    public DeadlineService(DeadlineStore store, ScheduleStore schedule, ErrorLog log)
    {
        _store = store;
        _log = log;
        _list = store.Load();
        Schedule = schedule.Load();
    }

    /// <summary>Raised on the UI thread after any change.</summary>
    public event EventHandler? Changed;

    /// <summary>The weekly class schedule (empty until the school sync in Phase 5 fills it).</summary>
    public IReadOnlyList<ClassPeriod> Schedule { get; }

    public IReadOnlyList<Deadline> Upcoming() => _list.Upcoming();

    public IReadOnlyList<Deadline> Completed() => _list.Completed();

    public Deadline? Find(Guid id) => _list.Find(id);

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
