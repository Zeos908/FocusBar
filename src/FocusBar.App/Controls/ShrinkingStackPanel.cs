using System.Windows;
using System.Windows.Controls;

namespace FocusBar.App.Controls;

/// <summary>
/// Lays children out in a row at <see cref="ItemWidth"/> each, and squeezes them evenly (down to
/// <see cref="MinItemWidth"/>) when there isn't room, so 20+ open windows still fit on the bar.
/// Anything that still doesn't fit is clipped.
/// </summary>
public sealed class ShrinkingStackPanel : Panel
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(ShrinkingStackPanel),
        new FrameworkPropertyMetadata(44.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(ShrinkingStackPanel),
        new FrameworkPropertyMetadata(28.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public ShrinkingStackPanel()
    {
        ClipToBounds = true;
    }

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    private double WidthEach(double available)
    {
        var count = InternalChildren.Count;
        if (count == 0) return ItemWidth;
        if (double.IsInfinity(available) || count * ItemWidth <= available) return ItemWidth;
        return Math.Max(MinItemWidth, Math.Floor(available / count));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var each = WidthEach(availableSize.Width);
        double height = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(each, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        var width = each * InternalChildren.Count;
        if (!double.IsInfinity(availableSize.Width)) width = Math.Min(width, availableSize.Width);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var each = WidthEach(finalSize.Width);
        double x = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(new Rect(x, 0, each, finalSize.Height));
            x += each;
        }
        return finalSize;
    }
}
