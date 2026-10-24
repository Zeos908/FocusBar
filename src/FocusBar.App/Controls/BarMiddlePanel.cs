using System.Windows;
using System.Windows.Controls;

namespace FocusBar.App.Controls;

/// <summary>
/// Holds the deadline ticker (first child, on the left) and the app icons (second child, on the right, next to the
/// tray). The apps get the space they need, but never so much that the ticker drops below <see cref="ReservedWidth"/>
/// (with lots of windows open, the window buttons shrink instead); the ticker fills whatever is left.
/// </summary>
public sealed class BarMiddlePanel : Panel
{
    public static readonly DependencyProperty ReservedWidthProperty = DependencyProperty.Register(
        nameof(ReservedWidth), typeof(double), typeof(BarMiddlePanel),
        new FrameworkPropertyMetadata(240.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ReservedWidth
    {
        get => (double)GetValue(ReservedWidthProperty);
        set => SetValue(ReservedWidthProperty, value);
    }

    private UIElement? Ticker => InternalChildren.Count > 0 ? InternalChildren[0] : null;
    private UIElement? Apps => InternalChildren.Count > 1 ? InternalChildren[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var total = double.IsInfinity(availableSize.Width) ? ReservedWidth * 3 : availableSize.Width;

        Apps?.Measure(new Size(Math.Max(0, total - ReservedWidth), availableSize.Height));
        var appsWidth = Apps?.DesiredSize.Width ?? 0;
        Ticker?.Measure(new Size(Math.Max(0, total - appsWidth), availableSize.Height));

        var height = Math.Max(Ticker?.DesiredSize.Height ?? 0, Apps?.DesiredSize.Height ?? 0);
        return new Size(double.IsInfinity(availableSize.Width) ? appsWidth + (Ticker?.DesiredSize.Width ?? 0) : total, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var appsWidth = Math.Min(Apps?.DesiredSize.Width ?? 0, Math.Max(0, finalSize.Width - ReservedWidth));
        Ticker?.Arrange(new Rect(0, 0, Math.Max(0, finalSize.Width - appsWidth), finalSize.Height));
        Apps?.Arrange(new Rect(finalSize.Width - appsWidth, 0, appsWidth, finalSize.Height));
        return finalSize;
    }
}
