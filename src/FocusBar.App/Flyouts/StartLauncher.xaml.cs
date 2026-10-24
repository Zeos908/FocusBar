using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusBar.App.Services;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Launcher;
using ManagedShell.Common.Helpers;

namespace FocusBar.App.Flyouts;

/// <summary>
/// The Start launcher: search installed apps, Enter or click to open, right-click to pin.
/// </summary>
public partial class StartLauncher : FlyoutWindow
{
    private readonly AppCatalog _catalog;
    private readonly PinService _pins;
    private readonly ErrorLog _log;
    private List<LauncherItem> _all = [];

    internal StartLauncher(AppCatalog catalog, PinService pins, ErrorLog log)
    {
        _catalog = catalog;
        _pins = pins;
        _log = log;
        InitializeComponent();

        SetApps(_catalog.Latest);
        Loaded += async (_, _) =>
        {
            SearchBox.Focus();
            var fresh = await _catalog.RefreshAsync();
            if (IsLoaded) SetApps(fresh);
        };
    }

    private void SetApps(IReadOnlyList<AppEntry> apps)
    {
        if (_all.Count > 0 && _all.Select(item => item.Entry).SequenceEqual(apps)) return;

        // Reuse existing items so icons that already loaded survive a refresh.
        var existing = _all.ToDictionary(item => item.Entry.Path, StringComparer.OrdinalIgnoreCase);
        _all = [.. apps.Select(app => existing.TryGetValue(app.Path, out var item) && item.Entry == app
            ? item
            : new LauncherItem(app, _pins.IsPinned(app.Path)))];
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var byPath = _all.ToDictionary(item => item.Entry.Path, StringComparer.OrdinalIgnoreCase);
        var matches = AppSearch.Filter(_all.Select(item => item.Entry), SearchBox.Text)
            .Select(entry => byPath[entry.Path])
            .ToList();

        AppList.ItemsSource = matches;
        if (matches.Count > 0)
        {
            AppList.SelectedIndex = 0;
            AppList.ScrollIntoView(matches[0]);
        }

        EmptyText.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = _all.Count == 0
            ? "Looking for apps…"
            : $"No apps match \"{SearchBox.Text.Trim()}\".\nTry the Windows Start menu for Store apps.";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                LaunchSelected();
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.PageDown:
                MoveSelection(8);
                e.Handled = true;
                break;
            case Key.PageUp:
                MoveSelection(-8);
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int offset)
    {
        if (AppList.Items.Count == 0) return;
        AppList.SelectedIndex = Math.Clamp(AppList.SelectedIndex + offset, 0, AppList.Items.Count - 1);
        AppList.ScrollIntoView(AppList.SelectedItem);
    }

    private void AppList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LaunchSelected();
            e.Handled = true;
        }
    }

    private void AppList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Only when the click landed on a row, not on the scroll bar or empty space.
        if (ItemsControl.ContainerFromElement(AppList, (DependencyObject)e.OriginalSource) is ListBoxItem)
        {
            LaunchSelected();
        }
    }

    private void LaunchSelected()
    {
        if (AppList.SelectedItem is not LauncherItem item) return;

        CloseFlyout();
        if (!AppLauncher.Launch(item.Entry.Path, _log))
        {
            MessageBox.Show($"Couldn't open {item.Entry.Name}. It may have been moved or uninstalled.",
                "FocusBar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AppList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(AppList, (DependencyObject)e.OriginalSource) is not ListBoxItem { DataContext: LauncherItem item })
        {
            e.Handled = true; // right-click on empty space: no menu
            return;
        }

        AppList.SelectedItem = item;
        PinMenuItem.Header = item.IsPinned ? "Unpin from FocusBar" : "Pin to FocusBar";
    }

    private void PinMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (AppList.SelectedItem is not LauncherItem item) return;

        if (item.IsPinned)
        {
            _pins.Unpin(item.Entry.Path);
        }
        else
        {
            _pins.Pin(_pins.ForShortcut(item.Entry.Path, item.Entry.Name));
        }
        item.IsPinned = _pins.IsPinned(item.Entry.Path);
        StatusText.Text = item.IsPinned ? $"Pinned {item.Entry.Name}" : $"Unpinned {item.Entry.Name}";
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e) => LaunchSelected();

    private void OpenWindowsStart_Click(object sender, RoutedEventArgs e)
    {
        CloseFlyout();
        ShellHelper.ShowStartMenu();
    }
}

/// <summary>One row in the Start launcher.</summary>
internal sealed partial class LauncherItem : ObservableObject
{
    private bool _iconRequested;
    private ImageSource? _icon;

    [ObservableProperty]
    private bool _isPinned;

    public LauncherItem(AppEntry entry, bool isPinned)
    {
        Entry = entry;
        _isPinned = isPinned;
    }

    public AppEntry Entry { get; }
    public string Name => Entry.Name;
    public string Path => Entry.Path;

    /// <summary>Loads in the background the first time something displays it.</summary>
    public ImageSource? Icon
    {
        get
        {
            if (!_iconRequested) LoadIcon();
            return _icon;
        }
        private set => SetProperty(ref _icon, value);
    }

    private async void LoadIcon()
    {
        _iconRequested = true;
        Icon = await IconLoader.LoadAsync(Path);
    }
}
