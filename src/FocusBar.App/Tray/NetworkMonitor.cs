using System.Windows.Threading;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Status;
using Windows.Networking.Connectivity;

namespace FocusBar.App.Tray;

internal readonly record struct NetworkState(NetworkKind Kind, bool HasInternet, int? SignalBars, string? Name);

/// <summary>
/// Watches the internet connection using the Windows network APIs. Windows announces connects and
/// disconnects; Wi-Fi signal strength isn't announced, so it's re-read every 30 seconds while on Wi-Fi.
/// </summary>
internal sealed class NetworkMonitor : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ErrorLog _log;
    private readonly DispatcherTimer _signalTimer;
    private bool _disposed;

    public NetworkMonitor(Dispatcher dispatcher, ErrorLog log)
    {
        _dispatcher = dispatcher;
        _log = log;
        _signalTimer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) => Refresh(), dispatcher);

        try
        {
            NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not subscribe to network changes; the Wi-Fi icon will update every 30 seconds.");
            _signalTimer.Start();
        }

        Refresh();
    }

    public event Action<NetworkState>? Changed;

    public NetworkState Current { get; private set; } = new(NetworkKind.None, false, null, null);

    // Windows raises this on a background thread.
    private void OnNetworkStatusChanged(object sender) => _dispatcher.BeginInvoke(Refresh);

    /// <summary>
    /// Reads the connection on a background thread (it can take a moment), then reports on the UI thread.
    /// Call on the UI thread.
    /// </summary>
    public async void Refresh()
    {
        if (_disposed) return;
        var state = await Task.Run(Read);
        if (_disposed) return;

        Current = state;
        if (state.Kind == NetworkKind.Wifi) _signalTimer.Start(); else _signalTimer.Stop();
        Changed?.Invoke(state);
    }

    private NetworkState Read()
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile is null) return new NetworkState(NetworkKind.None, false, null, null);

            var hasInternet = profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess;
            if (profile.IsWlanConnectionProfile)
            {
                return new NetworkState(NetworkKind.Wifi, hasInternet, profile.GetSignalBars(), profile.ProfileName);
            }
            if (profile.IsWwanConnectionProfile)
            {
                return new NetworkState(NetworkKind.Cellular, hasInternet, profile.GetSignalBars(), profile.ProfileName);
            }
            return new NetworkState(NetworkKind.Ethernet, hasInternet, null, profile.ProfileName);
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not read the network connection.");
            return Current;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _signalTimer.Stop();
        try { NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged; }
        catch { /* already gone */ }
    }
}
