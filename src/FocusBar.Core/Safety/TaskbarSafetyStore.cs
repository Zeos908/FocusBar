using FocusBar.Core.Storage;

namespace FocusBar.Core.Safety;

/// <summary>
/// Persists <see cref="TaskbarSafetyState"/> so the taskbar can be restored by the watchdog
/// process or by the next launch, even if the FocusBar process that hid it was killed.
/// </summary>
public sealed class TaskbarSafetyStore
{
    public const string FileName = "taskbar-state.json";

    private readonly JsonFileStore _store;

    public TaskbarSafetyStore(JsonFileStore store)
    {
        _store = store;
    }

    public TaskbarSafetyState? Load() => _store.Load<TaskbarSafetyState?>(FileName, null);

    public void Save(TaskbarSafetyState state) => _store.Save(FileName, state);

    public void Clear() => _store.Delete(FileName);

    /// <summary>
    /// True when a saved state exists but its owner is gone, meaning the taskbar was left hidden.
    /// </summary>
    /// <param name="isOwnerRunning">Given (process ID, start time), whether that exact process is still alive.</param>
    public static bool NeedsRecovery(TaskbarSafetyState? state, Func<int, DateTimeOffset, bool> isOwnerRunning) =>
        state is not null && !isOwnerRunning(state.OwnerProcessId, state.OwnerStartTime);

    /// <summary>
    /// True when <paramref name="state"/> was written by the given process, so that process
    /// (or its watchdog) is the one responsible for restoring the taskbar.
    /// </summary>
    public static bool IsOwnedBy(TaskbarSafetyState? state, int processId, DateTimeOffset startTime) =>
        state is not null && state.OwnerProcessId == processId && SameInstant(state.OwnerStartTime, startTime);

    /// <summary>Process start times round-trip through JSON; compare to the millisecond.</summary>
    public static bool SameInstant(DateTimeOffset a, DateTimeOffset b) =>
        Math.Abs((a - b).TotalMilliseconds) < 1;
}
