using System.Windows.Interop;
using FocusBar.Core.Diagnostics;
using static FocusBar.App.Native.Win32;

namespace FocusBar.App.Safety;

/// <summary>
/// Global Ctrl+Alt+Shift+F: restore the Windows taskbar and exit FocusBar immediately.
/// Uses its own hidden message-only window so it keeps working even while the bar window is being recreated.
/// </summary>
internal sealed class PanicHotkey : IDisposable
{
    public const string Description = "Ctrl+Alt+Shift+F";

    private const int HotkeyId = 0x4642; // "FB"
    private const uint VirtualKeyF = 0x46;

    private readonly HwndSource _window;
    private readonly Action _onPressed;

    public PanicHotkey(Action onPressed, ErrorLog log)
    {
        _onPressed = onPressed;
        _window = new HwndSource(new HwndSourceParameters("FocusBar.PanicHotkey")
        {
            ParentWindow = HWND_MESSAGE,
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        });
        _window.AddHook(WndProc);

        IsRegistered = RegisterHotKey(_window.Handle, HotkeyId, MOD_CONTROL | MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, VirtualKeyF);
        if (!IsRegistered)
        {
            log.Warning($"Could not register the {Description} panic hotkey (another app may be using it). " +
                        "Use the bar's right-click menu to restore the Windows taskbar instead.");
        }
    }

    public bool IsRegistered { get; }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            // Run after this message returns: exiting disposes this very window.
            _window.Dispatcher.BeginInvoke(_onPressed);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (IsRegistered) UnregisterHotKey(_window.Handle, HotkeyId);
        _window.RemoveHook(WndProc);
        _window.Dispose();
    }
}
