using System.Windows;
using System.Windows.Controls;
using FocusBar.App.Native;
using FocusBar.App.Shell;
using FocusBar.App.Themes;

namespace FocusBar.App.Settings;

/// <summary>FocusBar's settings. Only one copy is open at a time.</summary>
public partial class SettingsWindow : Window
{
    private static SettingsWindow? _open;

    private readonly Dictionary<string, Func<FrameworkElement>> _pageFactories;

    /// <summary>Page names for <see cref="ShowSingle"/>.</summary>
    public const string RemindersPageName = "Reminders";
    public const string SourcesPageName = "Sources";
    public const string FocusPageName = "Focus";

    private SettingsWindow(BarServices services)
    {
        InitializeComponent();

        _pageFactories = new()
        {
            [RemindersPageName] = () => new RemindersPage(services.Settings, services.Reminders),
            [SourcesPageName] = () => new SourcesPage(services.Sync, services.Log),
            [FocusPageName] = () => new FocusPage(services.Settings, services.Apps, services.Focus),
        };

        SourceInitialized += (_, _) => DarkTitleBar.Apply(this, !ThemeManager.IsLight);
        Closed += (_, _) => _open = null;
        Pages.SelectedIndex = 0;
    }

    /// <summary>Opens Settings (on a given page, if named), or brings the open window to the front.</summary>
    internal static void ShowSingle(BarServices services, string? page = null)
    {
        if (_open is null)
        {
            _open = new SettingsWindow(services);
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
