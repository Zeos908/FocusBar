using System.ComponentModel;
using System.Diagnostics;
using FocusBar.Core.Safety;

namespace FocusBar.App.Safety;

internal static class ProcessInfo
{
    public static DateTimeOffset StartTime(Process process) => new(process.StartTime);

    /// <summary>True if the process with this ID is alive and is the same process (same start time).</summary>
    public static bool IsRunning(int processId, DateTimeOffset startTime)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited && TaskbarSafetyStore.SameInstant(StartTime(process), startTime);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // ArgumentException: no such process. The others: it exited, or we can't inspect it,
            // which for a FocusBar process the same user started means it isn't ours.
            return false;
        }
    }
}
