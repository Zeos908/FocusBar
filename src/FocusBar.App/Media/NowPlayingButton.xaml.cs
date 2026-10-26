using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using FocusBar.App.Flyouts;
using FocusBar.App.Services;

namespace FocusBar.App.Media;

/// <summary>The Now playing chip on the bar.</summary>
public partial class NowPlayingButton : UserControl
{
    internal const string PlayGlyph = "";
    internal const string PauseGlyph = "";

    private NowPlayingService? _media;
    private SettingsService? _settings;
    private FlyoutToggle<NowPlayingFlyout>? _flyout;

    public NowPlayingButton()
    {
        InitializeComponent();
    }

    internal void Initialize(NowPlayingService media, SettingsService settings)
    {
        _media = media;
        _settings = settings;
        _flyout = new FlyoutToggle<NowPlayingFlyout>(() => new NowPlayingFlyout(media));
        media.Changed += OnChanged;
        settings.Changed += OnChanged;
        Refresh();
    }

    internal void Shutdown()
    {
        _flyout?.Close();
        if (_media is not null) _media.Changed -= OnChanged;
        if (_settings is not null) _settings.Changed -= OnChanged;
    }

    /// <summary>Below 40 px there's only room for one line (the title).</summary>
    internal void SetCompact(bool compact) => SubtitleText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (_settings?.Current.ShowNowPlaying != true || _media?.Current is not { } info)
        {
            Visibility = Visibility.Collapsed;
            _flyout?.Close();
            return;
        }

        Visibility = Visibility.Visible;
        TitleText.Text = info.Title;
        SubtitleText.Text = info.Subtitle;
        Cover.Source = info.Cover;
        CoverGlyph.Visibility = info.Cover is null ? Visibility.Visible : Visibility.Collapsed;

        var tooltip = string.IsNullOrEmpty(info.Subtitle) ? info.Title : $"{info.Title}\n{info.Subtitle}";
        InfoButton.ToolTip = $"{tooltip}\nPlaying in {info.AppName}";
        AutomationProperties.SetName(InfoButton, $"Now playing: {info.Title}, {info.Subtitle}");

        PlayPauseGlyph.Text = info.IsPlaying ? PauseGlyph : PlayGlyph;
        PlayPauseButton.IsEnabled = info.CanPlayPause;
        PlayPauseButton.ToolTip = info.IsPlaying ? "Pause" : "Play";
        AutomationProperties.SetName(PlayPauseButton, info.IsPlaying ? "Pause" : "Play");
    }

    private void Info_Click(object sender, RoutedEventArgs e) => _flyout?.Toggle(InfoButton, HorizontalAlignment.Center);

    private async void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_media is not null) await _media.TogglePlayPauseAsync();
    }
}
