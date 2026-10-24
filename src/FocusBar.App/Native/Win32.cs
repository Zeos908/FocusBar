using System.Runtime.InteropServices;

namespace FocusBar.App.Native;

/// <summary>
/// The few Win32 calls FocusBar makes itself. Docking, the task list, and the tray go through
/// ManagedShell; these are only for the safety net, which must keep working even if
/// ManagedShell is in a broken state (or in a separate watchdog process that never loads it).
/// </summary>
internal static class Win32
{
    public const string TaskbarClass = "Shell_TrayWnd";
    public const string SecondaryTaskbarClass = "Shell_SecondaryTrayWnd";

    // The classic Start button ("Button" class atom) that sits on top of the taskbar on some systems.
    public static readonly IntPtr StartButtonClassAtom = (IntPtr)0xC017;

    public const uint ABM_GETSTATE = 0x4;
    public const uint ABM_SETSTATE = 0xA;
    public const int ABS_AUTOHIDE = 0x1;
    public const int ABS_ALWAYSONTOP = 0x2;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_NOREPEAT = 0x4000;

    public static readonly IntPtr HWND_MESSAGE = new(-3);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [DllImport("shell32.dll")]
    public static extern UIntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll", EntryPoint = "FindWindowExW", SetLastError = true)]
    public static extern IntPtr FindWindowExAtom(IntPtr hWndParent, IntPtr hWndChildAfter, IntPtr lpszClassAtom, IntPtr lpszWindow);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
