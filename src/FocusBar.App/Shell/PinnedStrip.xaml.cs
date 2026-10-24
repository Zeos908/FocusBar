using System.Windows;
using System.Windows.Controls;
using FocusBar.App.Services;
using FocusBar.Core.Diagnostics;

namespace FocusBar.App.Shell;

/// <summary>Pinned app buttons on the bar.</summary>
public partial class PinnedStrip : UserControl
{
    private PinService? _pins;
    private ErrorLog? _log;

    public PinnedStrip()
    {
        InitializeComponent();
    }

    internal void Initialize(PinService pins, ErrorLog log)
    {
        _pins = pins;
        _log = log;
        Pins.ItemsSource = pins.Items;
    }

    private static PinnedAppItem? ItemOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as PinnedAppItem;

    private void PinButton_Click(object sender, RoutedEventArgs e) => Open(ItemOf(sender));

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e) => Open(ItemOf(sender));

    private void Open(PinnedAppItem? item)
    {
        if (item is null || _log is null || _pins is null) return;
        if (AppLauncher.Launch(item.Path, _log)) return;

        var unpin = MessageBox.Show(
            $"Couldn't open {item.Name}. It may have been moved or uninstalled.\n\nUnpin it from FocusBar?",
            "FocusBar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (unpin == MessageBoxResult.Yes) _pins.Unpin(item.Path);
    }

    private void PinButton_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (_pins is null || ItemOf(sender) is not { } item || sender is not Button { ContextMenu: { } menu }) return;

        var index = _pins.Items.IndexOf(item);
        ((MenuItem)menu.Items[2]).IsEnabled = index > 0;
        ((MenuItem)menu.Items[3]).IsEnabled = index < _pins.Items.Count - 1;
    }

    private void MoveLeft_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) _pins?.Move(item.Path, -1);
    }

    private void MoveRight_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) _pins?.Move(item.Path, 1);
    }

    private void Unpin_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) _pins?.Unpin(item.Path);
    }
}
