using System.Windows;
using System.Windows.Controls;

namespace FocusBar.App.Controls;

/// <summary>
/// Holds the open-windows list (first child) and the deadline ticker (second child). The windows list
/// gets the space it needs, but never so much that the ticker drops below <see cref="ReservedWidth"/>;
/// the ticker fills whatever is left.
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

    private UIElement? First => InternalChildren.Count > 0 ? InternalChildren[0] : null;
    private UIElement? Second => InternalChildren.Count > 1 ? InternalChildren[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var total = double.IsInfinity(availableSize.Width) ? ReservedWidth * 3 : availableSize.Width;

        First?.Measure(new Size(Math.Max(0, total - ReservedWidth), availableSize.Height));
        var firstWidth = First?.DesiredSize.Width ?? 0;
        Second?.Measure(new Size(Math.Max(0, total - firstWidth), availableSize.Height));

        var height = Math.Max(First?.DesiredSize.Height ?? 0, Second?.DesiredSize.Height ?? 0);
        return new Size(double.IsInfinity(availableSize.Width) ? firstWidth + (Second?.DesiredSize.Width ?? 0) : total, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var firstWidth = Math.Min(First?.DesiredSize.Width ?? 0, Math.Max(0, finalSize.Width - ReservedWidth));
        First?.Arrange(new Rect(0, 0, firstWidth, finalSize.Height));
        Second?.Arrange(new Rect(firstWidth, 0, Math.Max(0, finalSize.Width - firstWidth), finalSize.Height));
        return finalSize;
    }
}
