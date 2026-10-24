using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace FocusBar.App.Tray;

internal readonly record struct BatteryState(bool HasBattery, int? Percent, bool PluggedIn, bool Charging, TimeSpan? Remaining);

/// <summary>
/// Reads the battery level. Updated when Windows reports a power change (the bar forwards
/// WM_POWERBROADCAST here) and once a minute, which is how often the percentage can usefully change.
/// </summary>
internal sealed class BatteryMonitor : IDisposable
{
    private const byte AcOnline = 1;
    private const byte FlagCharging = 8;
    private const byte FlagNoBattery = 128;
    private const byte UnknownPercent = 255;
    private const uint UnknownLifetime = 0xFFFFFFFF;

    private readonly DispatcherTimer _timer;

    public BatteryMonitor(Dispatcher dispatcher)
    {
        _timer = new DispatcherTimer(TimeSpan.FromMinutes(1), DispatcherPriority.Background, (_, _) => Refresh(), dispatcher);
        _timer.Start();
        Refresh();
    }

    public event Action<BatteryState>? Changed;

    public BatteryState Current { get; private set; }

    public void Refresh()
    {
        var state = Read();
        if (state == Current) return;
        Current = state;
        Changed?.Invoke(state);
    }

    private static BatteryState Read()
    {
        if (!GetSystemPowerStatus(out var status) || (status.BatteryFlag & FlagNoBattery) != 0)
        {
            return new BatteryState(false, null, true, false, null);
        }

        int? percent = status.BatteryLifePercent == UnknownPercent ? null : status.BatteryLifePercent;
        TimeSpan? remaining = status.BatteryLifeTime == UnknownLifetime ? null : TimeSpan.FromSeconds(status.BatteryLifeTime);
        return new BatteryState(
            HasBattery: true,
            Percent: percent,
            PluggedIn: status.ACLineStatus == AcOnline,
            Charging: (status.BatteryFlag & FlagCharging) != 0,
            Remaining: remaining);
    }

    public void Dispose() => _timer.Stop();

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}
