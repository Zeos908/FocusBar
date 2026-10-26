using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Media;
using Windows.Media.Control;

namespace FocusBar.App.Services;

/// <summary>What's playing right now, ready to show.</summary>
internal sealed record NowPlayingInfo(
    string Title,
    string Subtitle,
    string? Album,
    string AppName,
    ImageSource? Cover,
    bool IsPlaying,
    bool CanPlayPause,
    bool CanPrevious,
    bool CanNext,
    TimeSpan Start,
    TimeSpan End,
    TimeSpan Position,
    DateTimeOffset PositionUpdated)
{
    public TimelineView? Timeline(DateTimeOffset now) => MediaText.Timeline(Start, End, Position, PositionUpdated, now, IsPlaying);
}

/// <summary>
/// The music or video Windows knows is playing (the same thing the volume flyout's media controls show): Spotify, a
/// YouTube tab, Media Player, and so on, through Windows' media session API. It only does work when something changes,
/// so it costs nothing while nothing's playing. Everything stays on this PC.
/// </summary>
internal sealed class NowPlayingService : IDisposable
{
    private readonly ErrorLog _log;
    private readonly Dispatcher _dispatcher;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string? _coverKey;     // which song the cover below belongs to
    private ImageSource? _cover;
    private bool _refreshQueued;
    private bool _disposed;

    public NowPlayingService(ErrorLog log, Dispatcher dispatcher)
    {
        _log = log;
        _dispatcher = dispatcher;
        _ = StartAsync();
    }

    /// <summary>Raised on the UI thread when what's playing changes (or stops).</summary>
    public event EventHandler? Changed;

    /// <summary>What's playing, or null when nothing is (or Windows doesn't support it).</summary>
    public NowPlayingInfo? Current { get; private set; }

    public Task TogglePlayPauseAsync() => Control(s => s.TryTogglePlayPauseAsync().AsTask(), "play/pause");

    public Task NextAsync() => Control(s => s.TrySkipNextAsync().AsTask(), "next");

    public Task PreviousAsync() => Control(s => s.TrySkipPreviousAsync().AsTask(), "previous");

    private async Task StartAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_disposed) return;
            _manager.CurrentSessionChanged += (_, _) => QueueRefresh();
            QueueRefresh();
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException or TypeLoadException)
        {
            // Older or trimmed-down Windows editions; the bar just doesn't show what's playing.
            _log.Write(ex, "Windows' media controls aren't available; Now playing is off.");
        }
    }

    /// <summary>Windows often sends several changes at once (new song: title, cover, and state); refresh once.</summary>
    private void QueueRefresh()
    {
        if (_refreshQueued || _disposed) return;
        _refreshQueued = true;
        _dispatcher.InvokeAsync(async () =>
        {
            _refreshQueued = false;
            await RefreshAsync();
        });
    }

    private async Task RefreshAsync()
    {
        if (_disposed || _manager is null) return;

        try
        {
            Watch(_manager.GetCurrentSession());
            if (_session is not { } session)
            {
                Publish(null);
                return;
            }

            var properties = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            if (_disposed || session != _session) return;

            var appName = MediaText.AppName(session.SourceAppUserModelId);
            var (title, subtitle) = MediaText.Lines(properties?.Title, properties?.Artist, properties?.AlbumTitle, appName);
            var controls = playback?.Controls;

            // The cover only needs loading when the song changes.
            var key = $"{session.SourceAppUserModelId}|{properties?.Title}|{properties?.Artist}|{properties?.AlbumTitle}";
            if (key != _coverKey)
            {
                _coverKey = key;
                _cover = properties?.Thumbnail is { } thumbnail ? await LoadCoverAsync(thumbnail) : null;
                if (_disposed || session != _session) return;
            }

            Publish(new NowPlayingInfo(
                title, subtitle, string.IsNullOrWhiteSpace(properties?.AlbumTitle) ? null : properties!.AlbumTitle, appName, _cover,
                IsPlaying: playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                CanPlayPause: controls is { } c && (c.IsPlayPauseToggleEnabled || c.IsPlayEnabled || c.IsPauseEnabled),
                CanPrevious: controls?.IsPreviousEnabled == true,
                CanNext: controls?.IsNextEnabled == true,
                timeline?.StartTime ?? TimeSpan.Zero, timeline?.EndTime ?? TimeSpan.Zero, timeline?.Position ?? TimeSpan.Zero,
                timeline?.LastUpdatedTime ?? DateTimeOffset.MinValue));
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ObjectDisposedException)
        {
            // The app closed or its session ended while we were asking.
            _log.Write(ex, "Could not read what's playing.");
            Publish(null);
        }
    }

    /// <summary>Follows the session Windows considers current, and stops following the previous one.</summary>
    private void Watch(GlobalSystemMediaTransportControlsSession? session)
    {
        if (session == _session) return;
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnSessionChanged;
            _session.PlaybackInfoChanged -= OnSessionChanged;
            _session.TimelinePropertiesChanged -= OnSessionChanged;
        }
        _session = session;
        _coverKey = null;
        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnSessionChanged;
            _session.PlaybackInfoChanged += OnSessionChanged;
            _session.TimelinePropertiesChanged += OnSessionChanged;
        }
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSession sender, object args) => QueueRefresh();

    private void Publish(NowPlayingInfo? info)
    {
        if (info == Current) return;
        Current = info;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task<ImageSource?> LoadCoverAsync(Windows.Storage.Streams.IRandomAccessStreamReference thumbnail)
    {
        try
        {
            using var winStream = await thumbnail.OpenReadAsync();
            using var stream = winStream.AsStreamForRead();
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = buffer;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = 160; // the flyout shows it at 120; small enough to keep memory tiny
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is COMException or IOException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            _log.Write(ex, "Could not load the cover art for what's playing.");
            return null;
        }
    }

    private async Task Control(Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action, string what)
    {
        if (_session is not { } session) return;
        try
        {
            await action(session);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            _log.Write(ex, $"Could not send {what} to {Current?.AppName ?? "the player"}.");
        }
    }

    public void Dispose()
    {
        _disposed = true;
        Watch(null);
    }
}
