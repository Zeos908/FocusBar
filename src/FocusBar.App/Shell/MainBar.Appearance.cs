using System.Windows;
using System.Windows.Media;
using FocusBar.App.Themes;
using FocusBar.Core.Appearance;
using FocusBar.Core.Models;

namespace FocusBar.App.Shell;

/// <summary>The bar's half of Phase 7: shows the current theme and follows every change to it right away.</summary>
public partial class MainBar
{
    /// <summary>Text and hover colors that have to match the bar's own color (not just Windows' light/dark mode).</summary>
    private static readonly string[] PaletteKeys =
        ["BarForeground", "BarMutedForeground", "BarHover", "BarPressed", "BarPlaceholderBorder", "IndicatorOpen"];

    private static readonly Color DarkImageBase = Color.FromRgb(0x1F, 0x1F, 0x23);

    private bool _appearanceShutDown;
    private int _imageVersion;
    private string? _backgroundKey;
    private string? _startKey;

    private void InitializeAppearance()
    {
        _services.Appearance.Changed += OnAppearanceChanged;
        DpiChanged += (_, _) => ApplyTheme();
        SizeChanged += (_, e) =>
        {
            if (e.WidthChanged || e.HeightChanged) LoadImages(_services.Appearance.Theme);
        };
        ApplyTheme();
    }

    private void ShutdownAppearance()
    {
        _appearanceShutDown = true;
        _services.Appearance.Changed -= OnAppearanceChanged;
    }

    /// <summary>
    /// True when <paramref name="theme"/> switches between see-through and solid, which needs a new window
    /// (WPF can't change <see cref="Window.AllowsTransparency"/> on an open window).
    /// </summary>
    internal bool NeedsNewWindow(Theme theme) => (theme.Opacity < 1) != AllowsTransparency;

    private void OnAppearanceChanged(object? sender, EventArgs e)
    {
        // The app may have just replaced this bar with a new one during the same change.
        if (!_appearanceShutDown) ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (_appearanceShutDown) return;
        var theme = _services.Appearance.Theme;

        FontFamily = new FontFamily($"{theme.FontFamily}, Segoe UI");
        DateText.Visibility = theme.BarHeight >= 40 ? Visibility.Visible : Visibility.Collapsed; // two lines don't fit below that

        var hasImage = _services.Appearance.ImagePath(theme.BackgroundImagePath) is not null;
        ApplyColors(theme, hasImage);

        if (DesiredHeight != theme.BarHeight)
        {
            DesiredHeight = theme.BarHeight;
            if (IsLoaded) UpdatePosition(); // re-reserves screen space at the new height
        }

        LoadImages(theme);
    }

    /// <summary>
    /// The bar color (or Windows' light/dark color) at the theme's opacity, with text colors that read well on it.
    /// A background image always gets light text on a dark base and a dark scrim, so any picture stays readable.
    /// </summary>
    private void ApplyColors(Theme theme, bool hasImage)
    {
        Color? barColor = null;
        bool? darkBar = null;
        if (ColorMath.TryParse(theme.BarColor, out var rgb))
        {
            barColor = Color.FromRgb(rgb.R, rgb.G, rgb.B);
            darkBar = ColorMath.IsDark(rgb);
        }
        if (hasImage)
        {
            if (darkBar != true) barColor = DarkImageBase;
            darkBar = true;
        }

        foreach (var key in PaletteKeys)
        {
            if (darkBar is { } dark) Resources[key] = ThemeManager.BarPalette(light: !dark)[key];
            else Resources.Remove(key); // follow Windows' light/dark mode
        }

        var baseColor = barColor ?? (Application.Current.Resources["BarBackground"] as SolidColorBrush)?.Color ?? DarkImageBase;
        var opacity = AllowsTransparency ? theme.Opacity : 1.0;
        var background = new SolidColorBrush(baseColor) { Opacity = opacity };
        background.Freeze();
        Resources["BarBackground"] = background;
        BackgroundLayer.Opacity = opacity;
    }

    /// <summary>Loads the background and Start images off the UI thread, at the exact size and scaling they'll show.</summary>
    private async void LoadImages(Theme theme)
    {
        if (_appearanceShutDown) return;

        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var width = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
        double height = theme.BarHeight;
        double iconSize = Math.Clamp(theme.BarHeight - 24, 16, 32);

        var backgroundPath = _services.Appearance.ImagePath(theme.BackgroundImagePath);
        var startPath = _services.Appearance.ImagePath(theme.StartIconPath);
        var backgroundKey = backgroundPath is null ? null : $"{backgroundPath}|{width}|{height}|{dpi}|{theme.BackgroundFit}|{theme.BackgroundCrop}";
        var startKey = startPath is null ? null : $"{startPath}|{iconSize}|{dpi}";
        if (backgroundKey == _backgroundKey && startKey == _startKey) return;

        var version = ++_imageVersion;
        var log = _services.Log;
        var loadBackground = backgroundPath is not null && backgroundKey != _backgroundKey;
        var loadStart = startPath is not null && startKey != _startKey;
        LoadedImage? background = null, start = null;
        try
        {
            (background, start) = await Task.Run(() => (
                loadBackground ? ThemeImageLoader.Load(backgroundPath!, width, height, dpi, theme.BackgroundFit, log, theme.BackgroundCrop) : null,
                loadStart ? ThemeImageLoader.Load(startPath!, iconSize, iconSize, dpi, ImageFit.Fit, log) : null));
        }
        catch (Exception ex)
        {
            log.Write(ex, "Could not load the theme's images.");
        }

        if (version != _imageVersion || _appearanceShutDown) return; // a newer change is on its way

        if (backgroundKey != _backgroundKey)
        {
            _backgroundKey = background is null ? null : backgroundKey;
            BackgroundImage.Source = background?.Bitmap;
            BackgroundImage.Width = background?.Width ?? 0;
            BackgroundImage.Height = background?.Height ?? 0;
            BackgroundLayer.Visibility = background is null ? Visibility.Collapsed : Visibility.Visible;
        }

        if (startKey != _startKey)
        {
            _startKey = start is null ? null : startKey;
            StartImage.Source = start?.Bitmap;
            StartImage.Width = start?.Width ?? 0;
            StartImage.Height = start?.Height ?? 0;
            StartImage.Visibility = start is null ? Visibility.Collapsed : Visibility.Visible;
            StartGlyph.Visibility = start is null ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
