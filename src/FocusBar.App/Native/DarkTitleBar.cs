using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FocusBar.App.Native;

/// <summary>
/// Makes a normal window's title bar dark in dark mode. Windows only does this for apps that ask.
/// Supported on Windows 10 20H1 and later; older versions ignore it.
/// </summary>
internal static class DarkTitleBar
{
    private const int DwmUseImmersiveDarkMode = 20;

    public static void Apply(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var value = dark ? 1 : 0;
        try
        {
            DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref value, sizeof(int));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
