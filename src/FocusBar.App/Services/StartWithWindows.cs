using System.IO;
using System.Security;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Settings;
using Microsoft.Win32;

namespace FocusBar.App.Services;

/// <summary>
/// Reads and changes the "Start with Windows" entry (see <see cref="StartupCommand"/>). Off unless the student turns
/// it on; FocusBar never adds it by itself.
/// </summary>
internal static class StartWithWindows
{
    public static string ExePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "FocusBar.exe");

    /// <summary>True if Windows will start this copy of FocusBar at sign-in.</summary>
    public static bool IsOn()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupCommand.RunKeyPath);
            return StartupCommand.PointsTo(key?.GetValue(StartupCommand.ValueName) as string, ExePath);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>True if the entry exists but was turned off in Task Manager → Startup apps.</summary>
    public static bool IsTurnedOffInTaskManager()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupCommand.ApprovedKeyPath);
            return StartupCommand.IsTurnedOffInTaskManager(key?.GetValue(StartupCommand.ValueName) as byte[]);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>Turns the entry on (pointing at this copy of FocusBar) or off. Returns false if Windows refused.</summary>
    public static bool Set(bool on, ErrorLog log)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(StartupCommand.RunKeyPath, writable: true);
            if (on) key.SetValue(StartupCommand.ValueName, StartupCommand.For(ExePath), RegistryValueKind.String);
            else key.DeleteValue(StartupCommand.ValueName, throwOnMissingValue: false);
            log.Info($"Start with Windows turned {(on ? "on" : "off")}.");
            return true;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            log.Write(ex, "Could not change the Start with Windows setting.");
            return false;
        }
    }
}
