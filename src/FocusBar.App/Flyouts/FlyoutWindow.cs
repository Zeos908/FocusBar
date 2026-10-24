using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FocusBar.App.Flyouts;

/// <summary>
/// Base for pop-up panels that open above the bar (Start launcher, calendar, hidden tray icons).
/// A real window rather than a WPF Popup, so text boxes inside it get keyboard focus.
/// Closes when the user clicks anywhere else (the window loses activation) or presses Esc.
/// </summary>
public class FlyoutWindow : Window
{
    private const double GapAboveBar = 0;

    private bool _closing;

    public FlyoutWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;
        SetResourceReference(FontFamilyProperty, "BarFont");
        SetResourceReference(ForegroundProperty, "FlyoutForeground");

        Deactivated += (_, _) => CloseFlyout();
        PreviewKeyDown += OnPreviewKeyDown;
        Closed += (_, _) => FlyoutClosed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? FlyoutClosed;

    /// <summary>
    /// Opens the flyout just above <paramref name="anchor"/> (a control on the bar), lined up with its
    /// left edge (or right edge for flyouts on the right side of the bar), and kept inside the bar's width.
    /// </summary>
    public void ShowAbove(FrameworkElement anchor, HorizontalAlignment alignment)
    {
        var bar = GetWindow(anchor) ?? throw new InvalidOperationException("Anchor must be on a window.");

        // The window only knows its size once shown. Show it near the anchor (so it opens on the bar's
        // monitor and DPI), measure, then move it into place before anything is drawn.
        var start = DeviceToDips(anchor, anchor.PointToScreen(new Point(0, 0)));
        Left = start.X;
        Top = start.Y - 200;
        Show();
        UpdateLayout();

        var source = PresentationSource.FromVisual(this);
        var fromDevice = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

        var anchorTopLeft = fromDevice.Transform(anchor.PointToScreen(new Point(0, 0)));
        var anchorBottomRight = fromDevice.Transform(anchor.PointToScreen(new Point(anchor.ActualWidth, anchor.ActualHeight)));
        var barTopLeft = fromDevice.Transform(bar.PointToScreen(new Point(0, 0)));
        var barRight = fromDevice.Transform(bar.PointToScreen(new Point(bar.ActualWidth, 0))).X;

        var left = alignment == HorizontalAlignment.Right
            ? anchorBottomRight.X - ActualWidth
            : anchorTopLeft.X;

        Left = Math.Max(barTopLeft.X, Math.Min(left, barRight - ActualWidth));
        Top = barTopLeft.Y - ActualHeight - GapAboveBar;

        Activate();
    }

    private static Point DeviceToDips(Visual visual, Point devicePoint)
    {
        var fromDevice = PresentationSource.FromVisual(visual)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        return fromDevice.Transform(devicePoint);
    }

    public void CloseFlyout()
    {
        if (_closing) return;
        _closing = true;
        // Deactivated can fire while the window is still handling input; close once that finishes.
        Dispatcher.BeginInvoke(Close);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseFlyout();
        }
    }
}

/// <summary>
/// Opens and closes one kind of flyout from a bar button. Clicking the button while its flyout is open
/// closes it (rather than closing it on mouse-down and immediately reopening it on click).
/// </summary>
public sealed class FlyoutToggle<T> where T : FlyoutWindow
{
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);

    private readonly Func<T> _create;
    private T? _open;
    private DateTime _closedAt = DateTime.MinValue;

    public FlyoutToggle(Func<T> create)
    {
        _create = create;
    }

    public T? Current => _open;

    public void Toggle(FrameworkElement anchor, HorizontalAlignment alignment)
    {
        if (_open is not null)
        {
            _open.CloseFlyout();
            return;
        }

        // The click that closed it (by deactivating it) was on this same button: leave it closed.
        if (DateTime.UtcNow - _closedAt < ReopenGuard) return;

        var flyout = _create();
        flyout.FlyoutClosed += (_, _) =>
        {
            _open = null;
            _closedAt = DateTime.UtcNow;
        };
        _open = flyout;
        flyout.ShowAbove(anchor, alignment);
    }

    public void Close() => _open?.CloseFlyout();
}
