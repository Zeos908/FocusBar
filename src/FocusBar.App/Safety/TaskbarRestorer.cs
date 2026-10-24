using System.Runtime.InteropServices;
using static FocusBar.App.Native.Win32;

namespace FocusBar.App.Safety;

/// <summary>
/// Shows the real Windows taskbar again and puts back its original auto-hide setting.
/// Deliberately independent of ManagedShell: the watchdog process and the crash handlers both use it,
/// and neither can count on ManagedShell still working.
/// </summary>
internal static class TaskbarRestorer
{
    /// <summary>Windows' own default: always on top, not auto-hidden.</summary>
    public const int DefaultAppBarState = ABS_ALWAYSONTOP;

    /// <summary>
    /// Reads Explorer's current taskbar state (auto-hide on/off), or null if Explorer's taskbar isn't running.
    /// </summary>
    public static int? GetState()
    {
        var taskbar = FindExplorerTaskbars(TaskbarClass).FirstOrDefault();
        if (taskbar == IntPtr.Zero) return null;

        var data = NewAppBarData(taskbar);
        return (int)SHAppBarMessage(ABM_GETSTATE, ref data).ToUInt32();
    }

    /// <summary>
    /// Sets the auto-hide state back to <paramref name="originalState"/> and makes every Explorer taskbar
    /// (primary and secondary monitors) visible. Returns false if no Explorer taskbar was found.
    /// Safe to call more than once.
    /// </summary>
    public static bool Restore(int originalState)
    {
        var primary = FindExplorerTaskbars(TaskbarClass).FirstOrDefault();
        if (primary == IntPtr.Zero) return false;

        var data = NewAppBarData(primary);
        data.lParam = (IntPtr)originalState;
        SHAppBarMessage(ABM_SETSTATE, ref data);

        var bars = FindExplorerTaskbars(TaskbarClass).Concat(FindExplorerTaskbars(SecondaryTaskbarClass));
        foreach (var bar in bars)
        {
            Show(bar);
        }

        var startButton = FindWindowExAtom(IntPtr.Zero, IntPtr.Zero, StartButtonClassAtom, IntPtr.Zero);
        if (startButton != IntPtr.Zero && IsOwnedByOtherProcess(startButton))
        {
            Show(startButton);
        }

        return true;
    }

    private static void Show(IntPtr hwnd)
    {
        if (!IsWindowVisible(hwnd))
        {
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_SHOWWINDOW | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    /// <summary>
    /// ManagedShell's tray service creates its own window with the <c>Shell_TrayWnd</c> class inside
    /// the FocusBar process, so skip any window this process owns.
    /// </summary>
    private static IEnumerable<IntPtr> FindExplorerTaskbars(string className)
    {
        var hwnd = IntPtr.Zero;
        while ((hwnd = FindWindowEx(IntPtr.Zero, hwnd, className, null)) != IntPtr.Zero)
        {
            if (IsOwnedByOtherProcess(hwnd)) yield return hwnd;
        }
    }

    private static bool IsOwnedByOtherProcess(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid != (uint)Environment.ProcessId;
    }

    private static APPBARDATA NewAppBarData(IntPtr hwnd) => new()
    {
        cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
        hWnd = hwnd,
    };
}
