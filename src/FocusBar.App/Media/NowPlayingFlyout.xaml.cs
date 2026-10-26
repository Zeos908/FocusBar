using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;
using FocusBar.App.Flyouts;
using FocusBar.App.Services;

namespace FocusBar.App.Media;

/// <summary>Opens from the Now playing chip: bigger cover, song details, progress, and previous / play-pause / next.</summary>
public partial class NowPlayingFlyout : FlyoutWindow
{
    private readonly NowPlayingService _media;
    // Moves the progress bar along while open; stops when closed, so it costs nothing otherwise.
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };

    internal NowPlayingFlyout(NowPlayingService media)
    {
        _media = media;
        InitializeComponent();

        media.Changed += OnChanged;
        _timer.Tick += (_, _) => UpdateTimeline();
        FlyoutClosed += (_, _) =>
        {
            _timer.Stop();
            media.Changed -= OnChanged;
        };
        Refresh();
        _timer.Start();
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (_media.Current is not { } info)
        {
            CloseFlyout();
            return;
        }

        TitleText.Text = info.Title;
        SubtitleText.Text = info.Subtitle;
        SubtitleText.Visibility = string.IsNullOrEmpty(info.Subtitle) ? Visibility.Collapsed : Visibility.Visible;
        AlbumText.Text = info.Album ?? "";
        AlbumText.Visibility = info.Album is null || info.Album == info.Subtitle ? Visibility.Collapsed : Visibility.Visible;
        AppText.Text = $"Playing in {info.AppName}";
        Cover.Source = info.Cover;
        CoverGlyph.Visibility = info.Cover is null ? Visibility.Visible : Visibility.Collapsed;

        PlayPauseGlyph.Text = info.IsPlaying ? NowPlayingButton.PauseGlyph : NowPlayingButton.PlayGlyph;
        PlayPauseButton.IsEnabled = info.CanPlayPause;
        PlayPauseButton.ToolTip = info.IsPlaying ? "Pause" : "Play";
        AutomationProperties.SetName(PlayPauseButton, info.IsPlaying ? "Pause" : "Play");
        PreviousButton.IsEnabled = info.CanPrevious;
        NextButton.IsEnabled = info.CanNext;

        UpdateTimeline();
    }

    private void UpdateTimeline()
    {
        if (_media.Current?.Timeline(DateTimeOffset.Now) is not { } timeline)
        {
            TimelinePanel.Visibility = Visibility.Collapsed;
            return;
        }

        TimelinePanel.Visibility = Visibility.Visible;
        ProgressScale.ScaleX = timeline.Fraction;
        ElapsedText.Text = timeline.Elapsed;
        TotalText.Text = timeline.Total;
    }

    private async void PlayPause_Click(object sender, RoutedEventArgs e) => await _media.TogglePlayPauseAsync();

    private async void Previous_Click(object sender, RoutedEventArgs e) => await _media.PreviousAsync();

    private async void Next_Click(object sender, RoutedEventArgs e) => await _media.NextAsync();
}
