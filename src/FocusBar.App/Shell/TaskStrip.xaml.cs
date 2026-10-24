using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FocusBar.App.Services;
using ManagedShell;
using ManagedShell.WindowsTasks;

namespace FocusBar.App.Shell;

/// <summary>The open-windows list, fed by ManagedShell's tasks service.</summary>
public partial class TaskStrip : UserControl
{
    private PinService? _pins;

    // Clicking the bar makes the bar the active window, so remember whether the window was active
    // when the mouse went down; that decides between "switch to it" and "minimize it". (Same approach as RetroBar.)
    private ApplicationWindow.WindowState _stateWhenPressed = ApplicationWindow.WindowState.Inactive;

    public TaskStrip()
    {
        InitializeComponent();
    }

    internal void Initialize(ShellManager shell, PinService pins)
    {
        _pins = pins;
        Windows.ItemsSource = shell.Tasks.GroupedWindows;
    }

    private static ApplicationWindow? WindowOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as ApplicationWindow;

    private void WindowButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _stateWhenPressed = WindowOf(sender)?.State ?? ApplicationWindow.WindowState.Inactive;
    }

    private void WindowButton_Click(object sender, RoutedEventArgs e)
    {
        var window = WindowOf(sender);
        if (window is null) return;

        if (_stateWhenPressed == ApplicationWindow.WindowState.Active && window.CanMinimize)
        {
            window.Minimize();
        }
        else
        {
            window.BringToFront();
        }
        // Keyboard presses (Enter/Space) have no mouse-down, so default back to "switch to it".
        _stateWhenPressed = ApplicationWindow.WindowState.Inactive;
    }

    private void WindowButton_MouseUp(object sender, MouseButtonEventArgs e)
    {
        // Middle-click closes, like on the Windows taskbar.
        if (e.ChangedButton != MouseButton.Middle) return;
        WindowOf(sender)?.Close();
        e.Handled = true;
    }

    private void WindowButton_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var window = WindowOf(sender);
        if (window is null || sender is not Button { ContextMenu: { } menu }) return;

        var pinItem = (MenuItem)menu.Items[0];
        var path = PinService.PathOf(window);
        if (_pins is null || string.IsNullOrWhiteSpace(path))
        {
            pinItem.IsEnabled = false;
            pinItem.Header = "Pin to FocusBar";
            return;
        }

        pinItem.IsEnabled = true;
        pinItem.Header = _pins.IsPinned(path) ? "Unpin from FocusBar" : "Pin to FocusBar";
    }

    private void PinMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = WindowOf(sender);
        if (window is null || _pins is null) return;

        var path = PinService.PathOf(window);
        if (string.IsNullOrWhiteSpace(path)) return;

        if (_pins.IsPinned(path))
        {
            _pins.Unpin(path);
        }
        else if (PinService.ForWindow(window) is { } pin)
        {
            _pins.Pin(pin);
        }
    }

    private void CloseMenuItem_Click(object sender, RoutedEventArgs e) => WindowOf(sender)?.Close();
}
