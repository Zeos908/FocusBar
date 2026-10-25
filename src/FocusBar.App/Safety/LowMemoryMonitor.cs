using System.Runtime.InteropServices;
using System.Windows.Threading;
using FocusBar.App.Services;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Safety;
using Microsoft.Toolkit.Uwp.Notifications;

namespace FocusBar.App.Safety;

/// <summary>
/// Checks how full the computer's memory (RAM) is every few seconds. When it stays above the student's threshold
/// (90% by default) for 30 seconds, FocusBar closes the normal way, giving the Windows taskbar back, so the apps
/// they're working in have more room. A notification says why.
/// </summary>
internal sealed class LowMemoryMonitor : IDisposable
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(5);

    private readonly SettingsService _settings;
    private readonly Action<string> _close;
    private readonly ErrorLog _log;
    private readonly DispatcherTimer _timer;
    private readonly LowMemoryRule _rule = new();
    private bool _closing;

    /// <param name="close">Exits FocusBar the normal way (restores the taskbar); gets the reason for the log.</param>
    public LowMemoryMonitor(SettingsService settings, Action<string> close, ErrorLog log, Dispatcher dispatcher)
    {
        _settings = settings;
        _close = close;
        _log = log;
        _timer = new DispatcherTimer(CheckEvery, DispatcherPriority.Background, (_, _) => Check(), dispatcher);
    }

    /// <summary>Percent of RAM in use right now, or NaN if Windows won't say.</summary>
    public static double UsedPercent()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        return GlobalMemoryStatusEx(ref status) ? LowMemoryRule.UsedPercent(status.TotalPhys, status.AvailPhys) : double.NaN;
    }

    private void Check()
    {
        if (_closing) return;
        var settings = _settings.Current.LowMemory;
        if (!settings.Enabled)
        {
            _rule.Reset();
            return;
        }

        var used = UsedPercent();
        if (!_rule.Observe(used, settings.ThresholdPercent, DateTimeOffset.Now)) return;

        _closing = true;
        _timer.Stop();
        var percent = $"{used:0}%";
        _log.Warning($"Memory use has been above {settings.ThresholdPercent}% for {LowMemoryRule.SustainFor.TotalSeconds:0} seconds ({percent} now); closing FocusBar.");
        Notify(percent);
        _close($"computer low on memory ({percent} of RAM in use)");
    }

    private void Notify(string percent)
    {
        try
        {
            new ToastContentBuilder()
                .AddArgument("action", "lowmemory")
                .AddText("FocusBar closed to free up memory")
                .AddText($"Your computer was using {percent} of its memory, so FocusBar closed and the Windows taskbar is back. Click here to start FocusBar again.")
                .AddAttributionText("FocusBar")
                .Show();
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not show the low-memory notification.");
        }
    }

    public void Dispose() => _timer.Stop();

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);
}
