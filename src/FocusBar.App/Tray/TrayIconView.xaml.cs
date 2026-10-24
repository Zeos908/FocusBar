using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ManagedShell.Common.Helpers;
using ManagedShell.Interop;
using ManagedNotifyIcon = ManagedShell.WindowsTray.NotifyIcon;

namespace FocusBar.App.Tray;

/// <summary>
/// Shows one app's tray icon and passes mouse input to that app, following the pattern RetroBar uses
/// with ManagedShell (left button is reported on release, like the Windows taskbar does).
/// </summary>
public partial class TrayIconView : UserControl
{
    public static readonly DependencyProperty ShowTitleProperty = DependencyProperty.Register(
        nameof(ShowTitle), typeof(bool), typeof(TrayIconView),
        new PropertyMetadata(false, (d, e) => ((TrayIconView)d).TitleText.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    public TrayIconView()
    {
        InitializeComponent();
    }

    /// <summary>Show the app's name next to the icon (used in the hidden-icons list).</summary>
    public bool ShowTitle
    {
        get => (bool)GetValue(ShowTitleProperty);
        set => SetValue(ShowTitleProperty, value);
    }

    /// <summary>Runs before a click is passed on, so the bar can tell Windows where the tray is.</summary>
    public static Action? BeforeClick { get; set; }

    private ManagedNotifyIcon? Icon => DataContext as ManagedNotifyIcon;

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e) => e.Handled = true;

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        BeforeClick?.Invoke();
        if (e.ChangedButton != MouseButton.Left)
        {
            Icon?.IconMouseDown(e.ChangedButton, MouseHelper.GetCursorPositionParam(), GetDoubleClickTime());
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var icon = Icon;
        if (icon is null) return;

        var position = MouseHelper.GetCursorPositionParam();
        var doubleClickTime = GetDoubleClickTime();
        if (e.ChangedButton == MouseButton.Left)
        {
            icon.IconMouseDown(e.ChangedButton, position, doubleClickTime);
        }
        icon.IconMouseUp(e.ChangedButton, position, doubleClickTime);
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        e.Handled = true;
        HitArea.SetResourceReference(Border.BackgroundProperty, ShowTitle ? "ItemHover" : "BarHover");

        var icon = Icon;
        if (icon is null) return;

        // Tell the app where its icon is on screen (in device pixels) so it can place pop-ups next to it.
        var topLeft = HitArea.PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(this);
        icon.Placement = new NativeMethods.Rect
        {
            Left = (int)topLeft.X,
            Top = (int)topLeft.Y,
            Right = (int)(HitArea.ActualWidth * dpi.DpiScaleX),
            Bottom = (int)(HitArea.ActualHeight * dpi.DpiScaleY),
        };
        icon.IconMouseEnter(MouseHelper.GetCursorPositionParam());
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        e.Handled = true;
        HitArea.Background = Brushes.Transparent;
        Icon?.IconMouseLeave(MouseHelper.GetCursorPositionParam());
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        e.Handled = true;
        Icon?.IconMouseMove(MouseHelper.GetCursorPositionParam());
    }

    [DllImport("user32.dll")]
    private static extern int GetDoubleClickTime();
}
