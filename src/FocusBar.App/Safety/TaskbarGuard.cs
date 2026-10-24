using System.Diagnostics;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Safety;

namespace FocusBar.App.Safety;

/// <summary>
/// Owns the promise "never leave the user without a taskbar".
///
/// Before the Windows taskbar is hidden, the guard writes <c>taskbar-state.json</c> recording the
/// taskbar's original state and which process hid it. That file is only deleted after a clean exit
/// has restored the taskbar. If it is still there when FocusBar's process is gone, something went
/// wrong, and the watchdog (or the next launch) restores the taskbar from it.
/// </summary>
internal sealed class TaskbarGuard
{
    private readonly TaskbarSafetyStore _store;
    private readonly ErrorLog _log;
    private int _originalState = TaskbarRestorer.DefaultAppBarState;
    private bool _armed; // true once the original state is recorded and the taskbar may be hidden

    public TaskbarGuard(TaskbarSafetyStore store, ErrorLog log)
    {
        _store = store;
        _log = log;
    }

    /// <summary>
    /// Called by FocusBar right before it hides the taskbar. Lets the guard stop that hiding
    /// (ManagedShell re-hides the taskbar every 100 ms) before restoring.
    /// </summary>
    public Action? StopHiding { get; set; }

    /// <summary>If a previous FocusBar left the taskbar hidden, restore it before doing anything else.</summary>
    public void RecoverFromPreviousRun()
    {
        var state = _store.Load();
        if (!TaskbarSafetyStore.NeedsRecovery(state, ProcessInfo.IsRunning)) return;

        _log.Warning($"Previous FocusBar (pid {state!.OwnerProcessId}) exited without restoring the taskbar. Restoring it now.");
        TaskbarRestorer.Restore(state.OriginalAppBarState, returnTrayIcons: true);
        _store.Clear();
    }

    /// <summary>Records the taskbar's current state so it can be restored however FocusBar exits.</summary>
    public void RecordBeforeHiding()
    {
        _originalState = TaskbarRestorer.GetState() ?? TaskbarRestorer.DefaultAppBarState;
        _armed = true;

        using var self = Process.GetCurrentProcess();
        _store.Save(new TaskbarSafetyState(
            OriginalAppBarState: _originalState,
            OwnerProcessId: self.Id,
            OwnerStartTime: ProcessInfo.StartTime(self),
            HiddenAt: DateTimeOffset.Now));
    }

    /// <summary>
    /// Normal exit: restore the taskbar and delete the state file. Call after ManagedShell has stopped hiding it.
    /// </summary>
    public void RestoreAndForget(string reason)
    {
        if (!_armed) return;
        RestoreNow(reason);
        try { _store.Clear(); }
        catch (Exception ex) { _log.Write(ex, "Could not delete taskbar-state.json."); }
    }

    /// <summary>
    /// Crash path: restore the taskbar but keep the state file. The process may not survive long enough
    /// to finish cleanly, so the watchdog will restore again (harmlessly) and then delete the file.
    /// </summary>
    public void EmergencyRestore(string reason)
    {
        try { StopHiding?.Invoke(); }
        catch (Exception ex) { _log.Write(ex, "Could not stop ManagedShell from hiding the taskbar."); }

        RestoreNow(reason);
    }

    private void RestoreNow(string reason)
    {
        // Nothing was hidden yet, so there is nothing to restore (and restoring would overwrite the
        // user's own auto-hide setting with the default).
        if (!_armed) return;

        try
        {
            var found = TaskbarRestorer.Restore(_originalState);
            _log.Info(found
                ? $"Restored the Windows taskbar ({reason})."
                : $"Wanted to restore the Windows taskbar ({reason}) but Explorer's taskbar wasn't found.");
        }
        catch (Exception ex)
        {
            _log.Write(ex, $"Failed to restore the Windows taskbar ({reason}).");
        }
    }
}
