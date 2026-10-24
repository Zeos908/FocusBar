using FocusBar.Core.Storage;

namespace FocusBar.Core.Reminders;

/// <summary>Saves <see cref="ReminderState"/> to <c>reminders.json</c>.</summary>
public sealed class ReminderStateStore
{
    public const string FileName = "reminders.json";

    private readonly JsonFileStore _store;

    public ReminderStateStore(JsonFileStore store)
    {
        _store = store;
    }

    public ReminderState Load()
    {
        var state = _store.Load<ReminderState?>(FileName, null) ?? new ReminderState();
        // A hand-edited file could contain nulls.
        state.Fired ??= [];
        state.FirstSeen ??= [];
        state.Snoozed ??= [];
        return state;
    }

    public void Save(ReminderState state) => _store.Save(FileName, state);
}
