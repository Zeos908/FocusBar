using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Focus;
using FocusBar.Core.Models;

namespace FocusBar.App.Focus;

/// <param name="Id">Process ID.</param>
/// <param name="Name">Process name, as on the block list ("Discord").</param>
/// <param name="DisplayName">Friendlier name from the program's file info ("Discord"), for messages.</param>
internal readonly record struct BlockedProcess(int Id, string Name, string DisplayName);

/// <summary>
/// Finds running programs that are on the block list, and closes them.
/// The build instructions suggest WMI's process-start events, but those need administrator rights and FocusBar
/// runs as a normal user, so this uses the instructions' fallback instead: a quick check every 2 seconds, and only
/// while Focus Mode is on (a scan takes a few milliseconds).
/// </summary>
internal static class AppBlocker
{
    private static readonly ConcurrentDictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase);

    public static List<BlockedProcess> FindBlocked(FocusSettings settings, IReadOnlySet<string> allowed)
    {
        var found = new List<BlockedProcess>();
        if (settings.BlockedApps.Count == 0) return found;

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                var name = process.ProcessName;
                if (process.Id == Environment.ProcessId || allowed.Contains(name) || !BlockList.IsBlocked(name, settings)) continue;
                found.Add(new BlockedProcess(process.Id, name, DisplayName(process)));
            }
        }
        return found;
    }

    /// <summary>
    /// Asks the program to close (as if its window's X were clicked), then ends it if it's still running a moment
    /// later. Programs running as administrator can't be closed by FocusBar; that's logged and skipped.
    /// </summary>
    public static Task CloseAsync(int processId, ErrorLog log) => Task.Run(() =>
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.CloseMainWindow() && process.WaitForExit(1500)) return;
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            log.Warning($"Focus Mode couldn't close process {processId}: {ex.Message}");
        }
    });

    private static string DisplayName(Process process) => DisplayNames.GetOrAdd(process.ProcessName, name =>
    {
        try
        {
            var description = process.MainModule?.FileVersionInfo.FileDescription;
            return string.IsNullOrWhiteSpace(description) ? name : description.Trim();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return name;
        }
    });
}
