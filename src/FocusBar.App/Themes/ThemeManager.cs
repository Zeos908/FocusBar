using System.Windows;
using System.Windows.Media;
using FocusBar.Core.Appearance;
using FocusBar.Core.Models;
using Microsoft.Win32;

namespace FocusBar.App.Themes;

/// <summary>
/// Follows the Windows light/dark setting (Settings → Personalization → Colors → "Choose your mode").
/// The taskbar follows the *Windows* mode rather than the app mode, so FocusBar does too.
/// </summary>
internal static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static ResourceDictionary? _current;

    public static bool IsLight { get; private set; }

    /// <summary>Loads the colors matching the current Windows mode. Safe to call again when it changes.</summary>
    public static void Apply()
    {
        var light = ReadSystemUsesLightTheme();
        if (_current is not null && light == IsLight) return;

        IsLight = light;
        var colors = Load(light ? "Light" : "Dark");

        var merged = Application.Current.Resources.MergedDictionaries;
        if (_current is not null) merged.Remove(_current);
        // Colors go first so the styles merged after them can refer to them.
        merged.Insert(0, colors);
        _current = colors;
    }

    /// <summary>
    /// Uses the theme's accent color everywhere (bar, flyouts, buttons), with readable text on it. The default
    /// accent keeps the light/dark-specific shades from Light.xaml and Dark.xaml.
    /// </summary>
    public static void ApplyAccent(string accent)
    {
        var resources = Application.Current.Resources;
        if (accent == Theme.DefaultAccent || !ColorMath.TryParse(accent, out var color))
        {
            resources.Remove("Accent");
            resources.Remove("AccentForeground");
            return;
        }

        resources["Accent"] = Frozen(new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)));
        resources["AccentForeground"] = Frozen(new SolidColorBrush(ColorMath.IsDark(color) ? Colors.White : Color.FromRgb(0x0B, 0x1A, 0x33)));
    }

    /// <summary>The bar colors (text, hover, etc.) from Light.xaml or Dark.xaml, for a bar with its own color.</summary>
    public static ResourceDictionary BarPalette(bool light) =>
        light ? _lightPalette ??= Load("Light") : _darkPalette ??= Load("Dark");

    private static ResourceDictionary? _lightPalette;
    private static ResourceDictionary? _darkPalette;

    private static ResourceDictionary Load(string name) =>
        new() { Source = new Uri($"pack://application:,,,/FocusBar;component/Themes/{name}.xaml") };

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    /// <summary>Missing value means Windows 10's default: a dark taskbar.</summary>
    private static bool ReadSystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }
}
