using System.Windows;
using System.Windows.Controls;
using FocusBar.App.Native;
using FocusBar.App.Services;
using FocusBar.App.Themes;

namespace FocusBar.App.Settings;

/// <summary>FocusBar's settings. Only one copy is open at a time.</summary>
public partial class SettingsWindow : Window
{
    private static SettingsWindow? _open;

    private readonly Dictionary<string, Func<FrameworkElement>> _pageFactories;

    private SettingsWindow(SettingsService settings, ReminderService reminders)
    {
        InitializeComponent();

        _pageFactories = new()
        {
            ["Reminders"] = () => new RemindersPage(settings, reminders),
        };

        SourceInitialized += (_, _) => DarkTitleBar.Apply(this, !ThemeManager.IsLight);
        Closed += (_, _) => _open = null;
        Pages.SelectedIndex = 0;
    }

    /// <summary>Opens Settings (on a given page, if named), or brings the open window to the front.</summary>
    internal static void ShowSingle(SettingsService settings, ReminderService reminders, string? page = null)
    {
        if (_open is null)
        {
            _open = new SettingsWindow(settings, reminders);
            _open.Show();
        }

        if (page is not null) _open.Select(page);
        if (_open.WindowState == WindowState.Minimized) _open.WindowState = WindowState.Normal;
        _open.Activate();
    }

    private void Select(string page)
    {
        foreach (ListBoxItem item in Pages.Items)
        {
            if (item.Tag as string == page) Pages.SelectedItem = item;
        }
    }

    private void Pages_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Pages.SelectedItem is ListBoxItem { Tag: string tag } && _pageFactories?.TryGetValue(tag, out var create) == true)
        {
            PageHost.Content = create();
        }
    }
}
