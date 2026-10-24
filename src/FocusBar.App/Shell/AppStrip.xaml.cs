using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FocusBar.App.Services;
using FocusBar.Core.Diagnostics;
using ManagedShell;
using ManagedShell.WindowsTasks;

namespace FocusBar.App.Shell;

/// <summary>The app buttons on the bar: pinned apps and open apps, one button per app.</summary>
public partial class AppStrip : UserControl
{
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);

    private AppButtons? _buttons;
    private PinService? _pins;
    private ErrorLog? _log;

    // Clicking the bar makes the bar the active window, so remember whether the window was active when the mouse
    // went down; that decides between "switch to it" and "minimize it". (Same approach as RetroBar.)
    private ApplicationWindow.WindowState _stateWhenPressed = ApplicationWindow.WindowState.Inactive;

    // The list of windows for an app with several. Clicking that app's button again closes it.
    private WindowListFlyout? _list;
    private string? _listKey;
    private DateTime _listClosedAt = DateTime.MinValue;

    public AppStrip()
    {
        InitializeComponent();
    }

    internal void Initialize(ShellManager shell, PinService pins, ErrorLog log)
    {
        _pins = pins;
        _log = log;
        _buttons = new AppButtons(shell, pins, Dispatcher);
        Buttons.ItemsSource = _buttons.Items;
    }

    internal void Shutdown()
    {
        _list?.CloseFlyout();
        _buttons?.Dispose();
    }

    private static AppButton? ButtonOf(object sender) => (sender as FrameworkElement)?.DataContext as AppButton;

    private void AppButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _stateWhenPressed = ButtonOf(sender) is { Count: 1 } button ? button.Windows[0].State : ApplicationWindow.WindowState.Inactive;
    }

    private void AppButton_Click(object sender, RoutedEventArgs e)
    {
        if (ButtonOf(sender) is not { } button) return;

        switch (button.Count)
        {
            case 0:
                Launch(button);
                break;
            case 1:
                var window = button.Windows[0];
                if (_stateWhenPressed == ApplicationWindow.WindowState.Active && window.CanMinimize) window.Minimize();
                else window.BringToFront();
                break;
            default:
                ToggleList(button, (FrameworkElement)sender);
                break;
        }
        // Keyboard presses (Enter/Space) have no mouse-down, so default back to "switch to it".
        _stateWhenPressed = ApplicationWindow.WindowState.Inactive;
    }

    private void AppButton_MouseUp(object sender, MouseButtonEventArgs e)
    {
        // Middle-click: close a single window (like the Windows taskbar's middle-click on a window), open a pinned app
        // that isn't running, or list the windows when there are several (so nothing closes by surprise).
        if (e.ChangedButton != MouseButton.Middle || ButtonOf(sender) is not { } button) return;
        e.Handled = true;
        switch (button.Count)
        {
            case 0: Launch(button); break;
            case 1: button.Windows[0].Close(); break;
            default: ToggleList(button, (FrameworkElement)sender); break;
        }
    }

    private void ToggleList(AppButton button, FrameworkElement anchor)
    {
        if (_list is not null)
        {
            var sameApp = _listKey == button.Key;
            _list.CloseFlyout();
            if (sameApp) return;
        }
        // The click that closed it (by deactivating it) was on this same button: leave it closed.
        else if (_listKey == button.Key && DateTime.UtcNow - _listClosedAt < ReopenGuard)
        {
            return;
        }

        var list = new WindowListFlyout(button);
        list.FlyoutClosed += (_, _) =>
        {
            if (_list == list) _list = null;
            _listClosedAt = DateTime.UtcNow;
        };
        _list = list;
        _listKey = button.Key;
        list.ShowAbove(anchor, HorizontalAlignment.Center);
    }

    /// <summary>Starts the app (or another window of it). A pinned app that can't start offers to unpin itself.</summary>
    private void Launch(AppButton button)
    {
        if (_log is null || _pins is null || button.LaunchPath is not { } path) return;
        if (AppLauncher.Launch(path, _log) || button.Pin is not { } pin) return;

        var unpin = MessageBox.Show(
            $"Couldn't open {pin.Name}. It may have been moved or uninstalled.\n\nUnpin it from FocusBar?",
            "FocusBar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (unpin == MessageBoxResult.Yes) _pins.Unpin(pin.Path);
    }

    // ----- Right-click menu -----

    private void AppButton_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (_pins is null || ButtonOf(sender) is not { } button || sender is not Button { ContextMenu: { } menu })
        {
            e.Handled = true;
            return;
        }

        menu.Items.Clear();

        if (button.LaunchPath is not null)
        {
            menu.Items.Add(Item(button.Count == 0 ? $"Open {button.Name}" : "Open new window", () => Launch(button)));
        }

        var pinPath = button.Pin?.Path ?? (button.Count > 0 ? PinService.PathOf(button.Windows[0]) : null);
        if (button.Pin is { } pin)
        {
            Separate(menu);
            menu.Items.Add(Item("Unpin from FocusBar", () => _pins.Unpin(pin.Path)));
            var index = _pins.Items.IndexOf(pin);
            menu.Items.Add(Item("Move left", () => _pins.Move(pin.Path, -1), enabled: index > 0));
            menu.Items.Add(Item("Move right", () => _pins.Move(pin.Path, 1), enabled: index < _pins.Items.Count - 1));
        }
        else if (!string.IsNullOrWhiteSpace(pinPath) && PinService.ForWindow(button.Windows[0]) is { } newPin)
        {
            Separate(menu);
            menu.Items.Add(Item("Pin to FocusBar", () => _pins.Pin(newPin)));
        }

        if (button.Count > 0)
        {
            Separate(menu);
            var windows = button.Windows.ToList();
            menu.Items.Add(Item(windows.Count == 1 ? "Close window" : $"Close all {windows.Count} windows",
                () => windows.ForEach(w => w.Close())));
        }

        if (menu.Items.Count == 0) e.Handled = true;
    }

    private static MenuItem Item(string header, Action click, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        item.Click += (_, _) => click();
        return item;
    }

    private static void Separate(ContextMenu menu)
    {
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
    }
}
