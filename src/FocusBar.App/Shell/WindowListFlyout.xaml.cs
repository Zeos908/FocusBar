using System.ComponentModel;
using System.Windows;
using FocusBar.App.Flyouts;
using ManagedShell.WindowsTasks;

namespace FocusBar.App.Shell;

/// <summary>Opens from an app button that has several windows: pick one to switch to it, or close one.</summary>
public partial class WindowListFlyout : FlyoutWindow
{
    private readonly AppButton _button;

    internal WindowListFlyout(AppButton button)
    {
        _button = button;
        DataContext = button;
        InitializeComponent();

        // Windows can close while the list is open; when none are left, there's nothing to pick.
        button.PropertyChanged += OnButtonChanged;
        FlyoutClosed += (_, _) => button.PropertyChanged -= OnButtonChanged;
    }

    private void OnButtonChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppButton.Count) && _button.Count == 0) CloseFlyout();
    }

    private static ApplicationWindow? WindowOf(object sender) => (sender as FrameworkElement)?.DataContext as ApplicationWindow;

    private void Window_Click(object sender, RoutedEventArgs e)
    {
        if (WindowOf(sender) is not { } window) return;
        CloseFlyout();
        window.BringToFront();
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => WindowOf(sender)?.Close();
}
