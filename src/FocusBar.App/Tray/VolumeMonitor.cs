using System.Runtime.InteropServices;
using System.Windows.Threading;
using FocusBar.Core.Diagnostics;

namespace FocusBar.App.Tray;

internal readonly record struct VolumeState(bool HasDevice, double Level, bool Muted);

/// <summary>
/// Reads and changes the speaker volume through Windows Core Audio, and listens for changes
/// (from the keyboard volume keys, other apps, or switching to headphones).
/// </summary>
internal sealed class VolumeMonitor : IDisposable
{
    private static readonly Guid AudioEndpointVolumeId = typeof(IAudioEndpointVolume).GUID;

    private readonly Dispatcher _dispatcher;
    private readonly ErrorLog _log;
    private readonly DefaultDeviceListener _deviceListener;
    private readonly VolumeChangeListener _volumeListener;
    private IMMDeviceEnumerator? _enumerator;
    private IAudioEndpointVolume? _endpoint;
    private bool _disposed;

    public VolumeMonitor(Dispatcher dispatcher, ErrorLog log)
    {
        _dispatcher = dispatcher;
        _log = log;
        _deviceListener = new DefaultDeviceListener(() => _dispatcher.BeginInvoke(() => { if (!_disposed) AttachToDefaultDevice(); }));
        _volumeListener = new VolumeChangeListener(() => _dispatcher.BeginInvoke(() => { if (!_disposed) Read(); }));

        try
        {
            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            _enumerator.RegisterEndpointNotificationCallback(_deviceListener);
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not connect to Windows audio; the volume icon won't update.");
        }

        AttachToDefaultDevice();
    }

    public event Action<VolumeState>? Changed;

    public VolumeState Current { get; private set; }

    /// <summary>Changes the volume by <paramref name="delta"/> (for example 0.02 for 2%), unmuting if needed.</summary>
    public void Adjust(double delta)
    {
        if (_endpoint is null) return;
        try
        {
            var context = Guid.Empty;
            Check(_endpoint.GetMasterVolumeLevelScalar(out var level));
            var target = (float)Math.Clamp(Math.Round((level + delta) * 50, MidpointRounding.AwayFromZero) / 50, 0, 1);
            Check(_endpoint.SetMasterVolumeLevelScalar(target, ref context));
            if (delta > 0 && Current.Muted) Check(_endpoint.SetMute(false, ref context));
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not change the volume.");
        }
        Read();
    }

    public void ToggleMute()
    {
        if (_endpoint is null) return;
        try
        {
            var context = Guid.Empty;
            Check(_endpoint.SetMute(!Current.Muted, ref context));
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not mute or unmute.");
        }
        Read();
    }

    private void AttachToDefaultDevice()
    {
        DetachFromDevice();
        if (_enumerator is null || _disposed)
        {
            Publish(new VolumeState(false, 0, false));
            return;
        }

        try
        {
            // E_NOTFOUND (no speakers or headphones) is normal on some PCs.
            if (_enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device) != 0 || device is null)
            {
                Publish(new VolumeState(false, 0, false));
                return;
            }

            var iid = AudioEndpointVolumeId;
            Check(device.Activate(ref iid, CoreAudio.ClsCtxAll, IntPtr.Zero, out var activated));
            _endpoint = (IAudioEndpointVolume)activated;
            Check(_endpoint.RegisterControlChangeNotify(_volumeListener));
            Marshal.ReleaseComObject(device);
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not read the default audio device.");
            _endpoint = null;
        }

        Read();
    }

    private void DetachFromDevice()
    {
        if (_endpoint is null) return;
        try { _endpoint.UnregisterControlChangeNotify(_volumeListener); } catch { }
        try { Marshal.ReleaseComObject(_endpoint); } catch { }
        _endpoint = null;
    }

    private void Read()
    {
        if (_endpoint is null)
        {
            Publish(new VolumeState(false, 0, false));
            return;
        }

        try
        {
            Check(_endpoint.GetMasterVolumeLevelScalar(out var level));
            Check(_endpoint.GetMute(out var muted));
            Publish(new VolumeState(true, level, muted));
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not read the volume.");
            Publish(new VolumeState(false, 0, false));
        }
    }

    private void Publish(VolumeState state)
    {
        Current = state;
        Changed?.Invoke(state);
    }

    private static void Check(int hresult) => Marshal.ThrowExceptionForHR(hresult);

    public void Dispose()
    {
        _disposed = true;
        DetachFromDevice();
        if (_enumerator is not null)
        {
            try { _enumerator.UnregisterEndpointNotificationCallback(_deviceListener); } catch { }
            try { Marshal.ReleaseComObject(_enumerator); } catch { }
            _enumerator = null;
        }
    }
}

// Windows calls these from its own audio threads. They only hand off to the UI thread and must never throw.

internal sealed class VolumeChangeListener(Action onChanged) : IAudioEndpointVolumeCallback
{
    public int OnNotify(IntPtr notifyData)
    {
        try { onChanged(); } catch { }
        return 0;
    }
}

internal sealed class DefaultDeviceListener(Action onDefaultSpeakersChanged) : IMMNotificationClient
{
    public int OnDefaultDeviceChanged(EDataFlow flow, ERole role, string? defaultDeviceId)
    {
        if (flow == EDataFlow.Render && role == ERole.Multimedia)
        {
            try { onDefaultSpeakersChanged(); } catch { }
        }
        return 0;
    }

    public int OnDeviceStateChanged(string? deviceId, int newState) => 0;
    public int OnDeviceAdded(string? deviceId) => 0;
    public int OnDeviceRemoved(string? deviceId) => 0;
    public int OnPropertyValueChanged(string? deviceId, PropertyKey key) => 0;
}
