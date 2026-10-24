using System.Diagnostics;
using FocusBar.Core.Diagnostics;
using ManagedShell.Common.Helpers;

namespace FocusBar.App.Services;

/// <summary>Starts apps, shortcuts, and Windows Settings pages. Never throws.</summary>
internal static class AppLauncher
{
    /// <summary>
    /// Opens a shortcut, program, or <c>appx:</c> Store app. Returns false (and logs why) if Windows couldn't open it.
    /// </summary>
    public static bool Launch(string path, ErrorLog log)
    {
        try
        {
            if (ShellHelper.StartProcess(path)) return true;
            log.Warning($"Windows could not open \"{path}\".");
        }
        catch (Exception ex)
        {
            log.Write(ex, $"Could not open \"{path}\".");
        }
        return false;
    }

    /// <summary>Opens a page in Windows Settings, such as <c>ms-settings:sound</c>.</summary>
    public static void OpenSettings(string uri, ErrorLog log)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            log.Write(ex, $"Could not open Windows Settings page {uri}.");
        }
    }
}
