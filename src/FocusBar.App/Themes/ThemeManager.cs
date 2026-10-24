using System.Windows;
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
        var colors = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/FocusBar;component/Themes/{(light ? "Light" : "Dark")}.xaml"),
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (_current is not null) merged.Remove(_current);
        // Colors go first so the styles merged after them can refer to them.
        merged.Insert(0, colors);
        _current = colors;
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
