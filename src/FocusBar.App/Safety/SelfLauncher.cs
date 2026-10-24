using System.Diagnostics;

namespace FocusBar.App.Safety;

/// <summary>Starts another copy of FocusBar (the watchdog, or a restart after a crash).</summary>
internal static class SelfLauncher
{
    public static void Start(string arguments)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Unknown executable path.");

        // Normally this is FocusBar.exe. If launched as "dotnet FocusBar.dll", relaunch the dll the same way.
        if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            arguments = $"\"{typeof(SelfLauncher).Assembly.Location}\" {arguments}";
        }

        Process.Start(new ProcessStartInfo(exe, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })?.Dispose();
    }
}
