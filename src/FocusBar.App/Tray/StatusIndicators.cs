using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusBar.App.Services;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Status;

namespace FocusBar.App.Tray;

/// <summary>
/// The Wi-Fi, volume, and battery icons on the bar. Each click opens the matching Windows Settings page.
/// </summary>
internal sealed partial class StatusIndicators : ObservableObject, IDisposable
{
    private const double VolumeStep = 0.02;

    private readonly ErrorLog _log;
    private readonly NetworkMonitor _network;
    private readonly VolumeMonitor _volume;
    private readonly BatteryMonitor _battery;

    [ObservableProperty] private string _networkGlyph = StatusGlyphs.NetworkOffline;
    [ObservableProperty] private string _networkTooltip = "";
    [ObservableProperty] private string _volumeGlyph = StatusGlyphs.Volume3;
    [ObservableProperty] private string _volumeTooltip = "";
    [ObservableProperty] private bool _hasAudioDevice;
    [ObservableProperty] private string _batteryGlyph = StatusGlyphs.BatteryUnknown;
    [ObservableProperty] private string _batteryTooltip = "";
    [ObservableProperty] private bool _hasBattery;

    public StatusIndicators(Dispatcher dispatcher, ErrorLog log)
    {
        _log = log;

        _network = new NetworkMonitor(dispatcher, log);
        _network.Changed += OnNetworkChanged;
        OnNetworkChanged(_network.Current);

        _volume = new VolumeMonitor(dispatcher, log);
        _volume.Changed += OnVolumeChanged;
        OnVolumeChanged(_volume.Current);

        _battery = new BatteryMonitor(dispatcher);
        _battery.Changed += OnBatteryChanged;
        OnBatteryChanged(_battery.Current);
    }

    /// <summary>Called when Windows broadcasts a power change (plugged in, unplugged, battery level).</summary>
    public void RefreshBattery() => _battery.Refresh();

    public void OpenNetworkSettings() => AppLauncher.OpenSettings(
        _network.Current.Kind == NetworkKind.Wifi ? "ms-settings:network-wifi" : "ms-settings:network-status", _log);

    public void OpenSoundSettings() => AppLauncher.OpenSettings("ms-settings:sound", _log);

    public void OpenBatterySettings() => AppLauncher.OpenSettings("ms-settings:batterysaver", _log);

    /// <summary>Mouse wheel over the volume icon: up is louder.</summary>
    public void ScrollVolume(int wheelDelta) => _volume.Adjust(wheelDelta > 0 ? VolumeStep : -VolumeStep);

    public void ToggleMute() => _volume.ToggleMute();

    private void OnNetworkChanged(NetworkState state)
    {
        NetworkGlyph = StatusGlyphs.Network(state.Kind, state.HasInternet, state.SignalBars);
        NetworkTooltip = StatusGlyphs.NetworkTooltip(state.Kind, state.HasInternet, state.Name);
    }

    private void OnVolumeChanged(VolumeState state)
    {
        HasAudioDevice = state.HasDevice;
        VolumeGlyph = state.HasDevice ? StatusGlyphs.Volume(state.Level, state.Muted) : StatusGlyphs.Mute;
        VolumeTooltip = state.HasDevice
            ? StatusGlyphs.VolumeTooltip(state.Level, state.Muted) + "\nScroll to change, middle-click to mute"
            : "No speakers or headphones found";
    }

    private void OnBatteryChanged(BatteryState state)
    {
        HasBattery = state.HasBattery;
        BatteryGlyph = StatusGlyphs.Battery(state.Percent, state.Charging);
        BatteryTooltip = StatusGlyphs.BatteryTooltip(state.Percent, state.PluggedIn, state.Charging, state.Remaining);
    }

    public void Dispose()
    {
        _network.Dispose();
        _volume.Dispose();
        _battery.Dispose();
    }
}
