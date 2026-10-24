using System.Diagnostics;
using System.Globalization;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Safety;

namespace FocusBar.App.Safety;

/// <summary>
/// A second, window-less copy of FocusBar.exe (<c>FocusBar.exe --watchdog &lt;pid&gt;</c>) that waits for the
/// main process to exit. If the main process was killed (for example from Task Manager) it can't run any
/// cleanup code, so the watchdog restores the taskbar on its behalf.
///
/// It never restarts FocusBar and never resists being closed: Focus Mode is a self-control tool, not anti-tamper.
/// If the watchdog is killed too (Task Manager's "End process tree"), the next FocusBar launch restores the
/// taskbar instead, or the user can run <c>FocusBar.exe --restore-taskbar</c>.
/// </summary>
internal static class Watchdog
{
    public const string Argument = "--watchdog";

    public static void Launch(ErrorLog log)
    {
        try
        {
            SelfLauncher.Start($"{Argument} {Environment.ProcessId}");
        }
        catch (Exception ex)
        {
            // Not fatal: normal exit, crash handlers, the panic hotkey, and the next launch still restore the taskbar.
            log.Write(ex, "Could not start the watchdog process. A killed FocusBar will be cleaned up on next launch.");
        }
    }

    /// <summary>
    /// If FocusBar died during Focus Mode, its website block is still in the hosts file. Remove it now
    /// (Windows asks for permission) rather than leaving sites blocked until FocusBar runs again.
    /// </summary>
    private static void LiftWebsiteBlocks(ErrorLog log)
    {
        if (!Focus.WebsiteBlocker.HasBlock()) return;
        log.Info("Removing Focus Mode's website block left behind by the crash.");
        Focus.WebsiteBlocker.Clear(log);
    }

    public static int Run(string[] args, TaskbarSafetyStore store, ErrorLog log)
    {
        if (args.Length < 2 || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parentId))
        {
            return 2;
        }

        Process parent;
        DateTimeOffset parentStart;
        try
        {
            parent = Process.GetProcessById(parentId);
            parentStart = ProcessInfo.StartTime(parent);
        }
        catch (Exception ex)
        {
            // FocusBar already exited before the watchdog got going. If it left its state file behind, clean up.
            log.Write(ex, $"Watchdog could not find FocusBar process {parentId}.");
            var leftover = store.Load();
            if (leftover?.OwnerProcessId == parentId)
            {
                TaskbarRestorer.Restore(leftover.OriginalAppBarState, returnTrayIcons: true);
                store.Clear();
                LiftWebsiteBlocks(log);
            }
            return 1;
        }

        using (parent)
        {
            parent.WaitForExit();
        }

        var state = store.Load();
        if (!TaskbarSafetyStore.IsOwnedBy(state, parentId, parentStart))
        {
            // Clean exit (file already deleted) or a newer FocusBar now owns the file.
            return 0;
        }

        log.Warning($"FocusBar (pid {parentId}) exited without restoring the taskbar. Watchdog is restoring it.");
        TaskbarRestorer.Restore(state!.OriginalAppBarState, returnTrayIcons: true);
        store.Clear();
        LiftWebsiteBlocks(log);
        return 0;
    }
}
