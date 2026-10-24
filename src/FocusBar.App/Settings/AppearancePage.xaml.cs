using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FocusBar.App.Focus;
using FocusBar.App.Services;
using FocusBar.App.Themes;
using FocusBar.Core.Appearance;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Models;
using Microsoft.Win32;

namespace FocusBar.App.Settings;

/// <summary>
/// Settings → Appearance: themes, pictures, colors, opacity, height, and font. Every change goes straight to the bar
/// (that's the live preview) and is saved.
/// </summary>
public partial class AppearancePage : UserControl
{
    private static readonly (string Hex, string Name)[] AccentColors =
    [
        ("#4F8EF7", "Blue"), ("#A78BFA", "Purple"), ("#EC4899", "Pink"), ("#EF4444", "Red"), ("#EA580C", "Orange"),
        ("#EAB308", "Yellow"), ("#22C55E", "Green"), ("#14B8A6", "Teal"), ("#64748B", "Slate"),
    ];

    private static readonly (string Hex, string Name)[] BarColors =
    [
        ("#1F1F23", "Charcoal"), ("#17132E", "Midnight"), ("#0B3B4F", "Ocean"), ("#1E3A2B", "Forest"), ("#3B0D2E", "Plum"),
        ("#EEF0F4", "Light gray"), ("#FFF1E0", "Sunrise"), ("#E0F2FE", "Sky"),
    ];

    private readonly AppearanceService _appearance;
    private readonly ErrorLog _log;
    private readonly List<string> _fonts;
    private readonly Dictionary<string, LoadedImage> _thumbnails = []; // small previews, so rebuilding the cards is cheap
    private bool _refreshing;
    private bool _draggingHeight;
    private FocusScreen? _preview;

    internal AppearancePage(AppearanceService appearance, ErrorLog log)
    {
        _appearance = appearance;
        _log = log;
        InitializeComponent();

        _fonts = [.. Fonts.SystemFontFamilies.Select(f => f.Source).Where(ThemeLibrary.IsValidFontName).Distinct()
            .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase)];
        FontList.ItemsSource = _fonts;

        BuildSwatches(AccentSwatches, "Accent", AccentColors, hex => _appearance.UpdateTheme(t => t with { AccentColor = hex }));
        BuildSwatches(BarSwatches, "BarColor", BarColors, hex => _appearance.UpdateTheme(t => t with { BarColor = hex }));

        appearance.Changed += OnChanged;
        Unloaded += (_, _) =>
        {
            appearance.Changed -= OnChanged;
            _preview?.Close();
        };
        Refresh();
    }

    private Theme Theme => _appearance.Theme;

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    /// <summary>Shows the current theme in every control (without those updates counting as changes).</summary>
    private void Refresh()
    {
        _refreshing = true;
        try
        {
            var settings = _appearance.Current;
            var theme = settings.Current;

            ThemeStatus.Text = settings.Source is null
                ? "Your own look. Save it as a theme to keep it in the list."
                : settings.HasChanges
                    ? $"Using {theme.Name}, with your changes. Save them as a theme to keep both."
                    : $"Using {theme.Name}.";
            BuildThemeCards(settings);

            (theme.BackgroundFit switch { ImageFit.Fit => FitOption, ImageFit.Center => CenterOption, _ => FillOption }).IsChecked = true;
            FitChips.IsEnabled = theme.BackgroundImagePath is not null;
            RemoveBackground.IsEnabled = theme.BackgroundImagePath is not null;
            RemoveStart.IsEnabled = theme.StartIconPath is not null;
            RemoveFocus.IsEnabled = theme.FocusScreenImagePath is not null;
            LoadThumbnails(theme);

            Select(AccentSwatches, theme.AccentColor);
            if (!AccentBox.IsKeyboardFocused) AccentBox.Text = theme.AccentColor;
            MatchWindowsOption.IsChecked = theme.BarColor is null;
            Select(BarSwatches, theme.BarColor);
            if (!BarColorBox.IsKeyboardFocused) BarColorBox.Text = theme.BarColor ?? "";

            OpacitySlider.Value = Math.Round(theme.Opacity * 100);
            OpacityValue.Text = $"{OpacitySlider.Value:0}%";
            if (!_draggingHeight)
            {
                HeightSlider.Value = theme.BarHeight;
                HeightValue.Text = $"{theme.BarHeight} px";
            }

            if (!Equals(FontList.SelectedItem, theme.FontFamily))
            {
                FontList.SelectedItem = _fonts.Contains(theme.FontFamily) ? theme.FontFamily : null;
                if (FontList.SelectedItem is not null) FontList.ScrollIntoView(FontList.SelectedItem);
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    // ----- Themes -----

    private void BuildThemeCards(ThemeSettings settings)
    {
        ThemeCards.Children.Clear();
        foreach (var theme in settings.All)
        {
            var isSource = settings.Source is { } source && source.Name.Equals(theme.Name, StringComparison.OrdinalIgnoreCase);
            ThemeCards.Children.Add(ThemeCard(theme, isSource, settings.HasChanges, BuiltInThemes.IsBuiltIn(theme.Name)));
        }
    }

    /// <summary>A small picture of the theme's bar, its name, and Use / Delete.</summary>
    private FrameworkElement ThemeCard(Theme theme, bool isSource, bool hasChanges, bool builtIn)
    {
        var barColor = ColorMath.TryParse(theme.BarColor, out var bar) ? Color.FromRgb(bar.R, bar.G, bar.B)
            : ThemeManager.IsLight ? Color.FromRgb(0xEE, 0xF0, 0xF4) : Color.FromRgb(0x1F, 0x1F, 0x23);
        var darkBar = !ColorMath.TryParse(theme.BarColor, out bar) ? !ThemeManager.IsLight : ColorMath.IsDark(bar);
        ColorMath.TryParse(theme.AccentColor, out var accent);

        var preview = new Border
        {
            Height = 30,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(barColor),
            Padding = new Thickness(8, 0, 8, 0),
            Child = new DockPanel
            {
                LastChildFill = false,
                Children =
                {
                    new System.Windows.Shapes.Rectangle
                    {
                        Width = 12, Height = 12, RadiusX = 2, RadiusY = 2, VerticalAlignment = VerticalAlignment.Center,
                        Fill = new SolidColorBrush(Color.FromRgb(accent.R, accent.G, accent.B)),
                    },
                    new TextBlock
                    {
                        Text = "Aa 9:41",
                        Margin = new Thickness(8, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new FontFamily($"{theme.FontFamily}, Segoe UI"),
                        FontSize = 12,
                        Foreground = darkBar ? Brushes.White : Brushes.Black,
                    },
                },
            },
        };
        if (theme.BackgroundImagePath is not null && _appearance.ImagePath(theme.BackgroundImagePath) is { } imagePath)
        {
            LoadInto(imagePath, 160, 30, ImageFit.Fill, image => preview.Background = new ImageBrush(image.Bitmap) { Stretch = Stretch.UniformToFill });
        }

        var use = new Button
        {
            Style = (Style)FindResource("FlyoutButton"),
            Padding = new Thickness(10, 3, 10, 3),
            Content = isSource ? hasChanges ? "Undo changes" : "In use" : "Use",
            IsEnabled = !isSource || hasChanges,
            Margin = new Thickness(0, 0, 6, 0),
        };
        AutomationProperties.SetName(use, $"{use.Content}: {theme.Name}");
        use.Click += (_, _) => _appearance.Update(s => ThemeLibrary.Apply(s, theme));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), Children = { use } };
        if (!builtIn)
        {
            var delete = new Button { Style = (Style)FindResource("FlyoutButton"), Padding = new Thickness(10, 3, 10, 3), Content = "Delete" };
            AutomationProperties.SetName(delete, $"Delete theme {theme.Name}");
            delete.Click += (_, _) => DeleteTheme(theme.Name);
            buttons.Children.Add(delete);
        }

        return new Border
        {
            Width = 176,
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(isSource ? 2 : 1),
            BorderBrush = (Brush)FindResource(isSource ? "Accent" : "InputBorder"),
            Background = (Brush)FindResource("ItemHover"),
            Child = new StackPanel
            {
                Children =
                {
                    preview,
                    new TextBlock
                    {
                        Text = builtIn ? $"{theme.Name} (built in)" : theme.Name,
                        Margin = new Thickness(0, 6, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        FontWeight = FontWeights.SemiBold,
                    },
                    buttons,
                },
            },
        };
    }

    private void SaveTheme_Click(object sender, RoutedEventArgs e) => SaveTheme();

    private void SaveNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        SaveTheme();
    }

    private void SaveTheme()
    {
        var name = SaveNameBox.Text;
        var problem = ThemeLibrary.NameProblem(name);
        if (problem is null && ThemeLibrary.WouldReplace(_appearance.Current, name))
        {
            var answer = MessageBox.Show(Window.GetWindow(this)!, $"Replace your theme \"{ThemeLibrary.NormalizeName(name)}\" with the current look?",
                "Save theme", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
        }

        if (problem is null)
        {
            try
            {
                _appearance.Update(s => ThemeLibrary.SaveAs(s, name));
                SaveNameBox.Clear();
            }
            catch (ArgumentException ex)
            {
                problem = ex.Message.Split(" (Parameter")[0];
            }
        }

        SaveError.Text = problem ?? "";
        SaveError.Visibility = problem is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void DeleteTheme(string name)
    {
        var answer = MessageBox.Show(Window.GetWindow(this)!, $"Delete the theme \"{name}\"? The bar keeps its current look.",
            "Delete theme", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes) _appearance.Update(s => ThemeLibrary.Delete(s, name));
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => _appearance.Update(s => ThemeLibrary.Apply(s, BuiltInThemes.Default));

    // ----- Pictures -----

    private void ChooseImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag } || !Enum.TryParse<ThemeImageKind>(tag, out var kind)) return;

        var dialog = new OpenFileDialog
        {
            Title = "Choose a picture",
            Filter = "Pictures (*.png, *.jpg, *.gif)|*.png;*.jpg;*.jpeg;*.gif",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        try
        {
            var name = _appearance.Import(dialog.FileName, kind);
            _appearance.UpdateTheme(t => kind switch
            {
                ThemeImageKind.Background => t with { BackgroundImagePath = name },
                ThemeImageKind.StartIcon => t with { StartIconPath = name },
                _ => t with { FocusScreenImagePath = name },
            });
            ShowImageError(null);
        }
        catch (ThemeImageException ex)
        {
            ShowImageError(ex.Message);
        }
    }

    private void RemoveImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag } || !Enum.TryParse<ThemeImageKind>(tag, out var kind)) return;
        _appearance.UpdateTheme(t => kind switch
        {
            ThemeImageKind.Background => t with { BackgroundImagePath = null },
            ThemeImageKind.StartIcon => t with { StartIconPath = null },
            _ => t with { FocusScreenImagePath = null },
        });
        ShowImageError(null);
    }

    private void ShowImageError(string? message)
    {
        ImageError.Text = message ?? "";
        ImageError.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Fit_Checked(object sender, RoutedEventArgs e)
    {
        if (_refreshing || sender is not FrameworkElement { Tag: string tag } || !Enum.TryParse<ImageFit>(tag, out var fit)) return;
        _appearance.UpdateTheme(t => t with { BackgroundFit = fit });
    }

    private void PreviewFocus_Click(object sender, RoutedEventArgs e)
    {
        _preview?.Close();
        _preview = new FocusScreen(_log);
        _preview.Closed += (_, _) => _preview = null;
        _preview.ShowPreview(_appearance.ImagePath(Theme.FocusScreenImagePath));
    }

    private void LoadThumbnails(Theme theme)
    {
        SetThumb(BackgroundThumb, theme.BackgroundImagePath, 120, 40);
        SetThumb(StartThumb, theme.StartIconPath, 28, 28);
        SetThumb(FocusThumb, theme.FocusScreenImagePath, 120, 40);
    }

    private void SetThumb(Image target, string? name, double width, double height)
    {
        var path = _appearance.ImagePath(name);
        if (Equals(target.Tag, path)) return; // already showing it
        target.Tag = path;
        target.Source = null;
        if (path is not null) LoadInto(path, width, height, ImageFit.Fill, image => { if (Equals(target.Tag, path)) target.Source = image.Bitmap; });
    }

    /// <summary>Decodes a small copy off the UI thread (once per picture and size), then hands it to <paramref name="show"/>.</summary>
    private async void LoadInto(string path, double width, double height, ImageFit fit, Action<LoadedImage> show)
    {
        var key = $"{path}|{width}x{height}|{fit}";
        if (_thumbnails.TryGetValue(key, out var cached))
        {
            show(cached);
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var log = _log;
        try
        {
            if (await Task.Run(() => ThemeImageLoader.Load(path, width, height, dpi, fit, log)) is { } image)
            {
                _thumbnails[key] = image;
                show(image);
            }
        }
        catch (Exception ex)
        {
            log.Write(ex, "Could not show a picture preview.");
        }
    }

    // ----- Colors -----

    private void BuildSwatches(Panel panel, string group, (string Hex, string Name)[] colors, Action<string> pick)
    {
        foreach (var (hex, name) in colors)
        {
            ColorMath.TryParse(hex, out var rgb);
            var swatch = new RadioButton
            {
                GroupName = group,
                Style = (Style)FindResource("ColorSwatch"),
                Background = new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B)),
                Tag = hex,
                ToolTip = $"{name} ({hex})",
            };
            AutomationProperties.SetName(swatch, name);
            swatch.Checked += (_, _) =>
            {
                if (!_refreshing) pick(hex);
            };
            panel.Children.Add(swatch);
        }
    }

    private static void Select(Panel panel, string? hex)
    {
        foreach (var swatch in panel.Children.OfType<RadioButton>()) swatch.IsChecked = Equals(swatch.Tag, hex);
    }

    private void AccentHex_Click(object sender, RoutedEventArgs e) => UseAccentHex();

    private void AccentBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        UseAccentHex();
    }

    private void UseAccentHex()
    {
        if (ColorMath.Normalize(AccentBox.Text) is { } hex)
        {
            ShowColorError(null);
            _appearance.UpdateTheme(t => t with { AccentColor = hex });
            AccentBox.Text = hex;
        }
        else
        {
            ShowColorError("Type a color code like #4F8EF7.");
        }
    }

    private void BarHex_Click(object sender, RoutedEventArgs e) => UseBarHex();

    private void BarColorBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        UseBarHex();
    }

    private void UseBarHex()
    {
        if (ColorMath.Normalize(BarColorBox.Text) is { } hex)
        {
            ShowColorError(null);
            _appearance.UpdateTheme(t => t with { BarColor = hex });
            BarColorBox.Text = hex;
        }
        else
        {
            ShowColorError("Type a color code like #1F1F23, or pick Match Windows.");
        }
    }

    private void MatchWindows_Checked(object sender, RoutedEventArgs e)
    {
        if (!_refreshing) _appearance.UpdateTheme(t => t with { BarColor = null });
    }

    private void ShowColorError(string? message)
    {
        ColorError.Text = message ?? "";
        ColorError.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
    }

    // ----- Size and text -----

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        OpacityValue.Text = $"{e.NewValue:0}%";
        if (!_refreshing) _appearance.UpdateTheme(t => t with { Opacity = e.NewValue / 100 });
    }

    /// <summary>
    /// The bar's height changes when the slider is let go (not on every step while dragging), because each change
    /// makes Windows move and resize maximized windows.
    /// </summary>
    private void HeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        HeightValue.Text = $"{e.NewValue:0} px";
        if (!_refreshing && !_draggingHeight) CommitHeight();
    }

    private void HeightSlider_DragStarted(object sender, DragStartedEventArgs e) => _draggingHeight = true;

    private void HeightSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _draggingHeight = false;
        CommitHeight();
    }

    private void CommitHeight()
    {
        var height = (int)Math.Round(HeightSlider.Value);
        _appearance.UpdateTheme(t => t with { BarHeight = height });
    }

    private void FontSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = FontSearch.Text.Trim();
        FontList.ItemsSource = query.Length == 0 ? _fonts : _fonts.Where(f => f.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
        if (_fonts.Contains(Theme.FontFamily))
        {
            _refreshing = true;
            FontList.SelectedItem = Theme.FontFamily;
            _refreshing = false;
        }
    }

    private void FontList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_refreshing && FontList.SelectedItem is string font) _appearance.UpdateTheme(t => t with { FontFamily = font });
    }
}
