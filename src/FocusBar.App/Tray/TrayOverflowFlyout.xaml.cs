using System.Windows;
using FocusBar.App.Flyouts;
using ManagedNotifyIcon = ManagedShell.WindowsTray.NotifyIcon;

namespace FocusBar.App.Tray;

/// <summary>Opens from ^ on the bar: every app's tray icon, with a button to pin or unpin each one.</summary>
public partial class TrayOverflowFlyout : FlyoutWindow
{
    private readonly TrayIcons _icons;

    internal TrayOverflowFlyout(TrayIcons icons)
    {
        _icons = icons;
        InitializeComponent();
        IconList.ItemsSource = icons.All;
    }

    private void TogglePin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ManagedNotifyIcon icon }) _icons.TogglePinned(icon);
    }
}
