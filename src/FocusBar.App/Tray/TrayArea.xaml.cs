using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FocusBar.App.Flyouts;

namespace FocusBar.App.Tray;

/// <summary>The right side of the bar: ^ (hidden icons), pinned tray icons, then Wi-Fi, volume, and battery.</summary>
public partial class TrayArea : UserControl
{
    private StatusIndicators? _status;
    private TrayIcons? _icons;
    private FlyoutToggle<TrayOverflowFlyout>? _overflow;

    public TrayArea()
    {
        InitializeComponent();
    }

    internal void Initialize(StatusIndicators status, TrayIcons? icons)
    {
        _status = status;
        _icons = icons;
        DataContext = status;

        if (icons is null || icons.IsFailed)
        {
            // Without the tray service there are no app icons to show; keep the Wi-Fi/volume/battery icons.
            OverflowButton.Visibility = Visibility.Collapsed;
            return;
        }

        PinnedIcons.ItemsSource = icons.Pinned;
        _overflow = new FlyoutToggle<TrayOverflowFlyout>(() => new TrayOverflowFlyout(icons));
        ((INotifyCollectionChanged)icons.All).CollectionChanged += (_, _) => UpdateOverflowButton();
        UpdateOverflowButton();
    }

    internal void CloseFlyouts() => _overflow?.Close();

    private void UpdateOverflowButton() =>
        OverflowButton.Visibility = _icons?.All.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OverflowButton_Click(object sender, RoutedEventArgs e) =>
        _overflow?.Toggle(OverflowButton, HorizontalAlignment.Left);

    private void Network_Click(object sender, RoutedEventArgs e) => _status?.OpenNetworkSettings();

    private void Volume_Click(object sender, RoutedEventArgs e) => _status?.OpenSoundSettings();

    private void Volume_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _status?.ScrollVolume(e.Delta);
        e.Handled = true;
    }

    private void Volume_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        _status?.ToggleMute();
        e.Handled = true;
    }

    private void Battery_Click(object sender, RoutedEventArgs e) => _status?.OpenBatterySettings();
}
