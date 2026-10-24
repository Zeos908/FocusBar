using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FocusBar.App.Native;
using FocusBar.App.Themes;
using FocusBar.Core.Appearance;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Models;

namespace FocusBar.App.Settings;

/// <summary>
/// Picks which part of a picture fills the bar: the picture sits behind a bar-shaped strip (the same shape as the real
/// bar), and the student drags it and zooms until the part they want is in the strip. The result is an
/// <see cref="ImageCrop"/>, which the bar uses at its real size and scaling (see <see cref="CropMath"/>).
/// </summary>
public partial class BackgroundEditor : Window
{
    private const double Gap = 40;         // space around the strip, so there's always picture to grab
    private const double WheelStep = 1.15; // one mouse-wheel notch or +/- press
    private const double KeyStep = 10;     // arrow keys move the picture this far

    private readonly PreviewImage? _image;
    private readonly double _barAspect;    // bar width / height
    private ImageCrop _crop;
    private Point? _dragFrom;
    private bool _updatingSlider;

    // The strip, in viewport coordinates.
    private double _frameX, _frameY, _frameW, _frameH;

    /// <param name="barWidth">The bar's size (any units); only its shape matters.</param>
    /// <param name="barHeight"></param>
    internal BackgroundEditor(string imagePath, ImageCrop? crop, double barWidth, double barHeight, ErrorLog log, Window owner)
    {
        Owner = owner;
        _barAspect = barWidth > 0 && barHeight > 0 ? barWidth / barHeight : 40;
        _crop = crop ?? ImageCrop.Default;
        InitializeComponent();

        // Big enough to look sharp when zoomed in, without holding a 40-megapixel photo in memory.
        _image = ThemeImageLoader.LoadPreview(imagePath, 4096, log);
        if (_image is null)
        {
            UseButton.IsEnabled = false;
            FrameLabel.Text = "FocusBar couldn't open this picture.";
        }
        else
        {
            Picture.Source = _image.Bitmap;
        }

        SourceInitialized += (_, _) => DarkTitleBar.Apply(this, !ThemeManager.IsLight);
        Loaded += (_, _) => Viewport.Focus();
    }

    /// <summary>The chosen part of the picture, once the student clicks "Use this picture".</summary>
    public ImageCrop? Result { get; private set; }

    // ----- Layout -----

    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e) => Arrange();

    /// <summary>Places the strip in the middle of the viewport, then the picture behind it.</summary>
    private void Arrange()
    {
        var (width, height) = (Viewport.ActualWidth, Viewport.ActualHeight);
        if (width <= 0 || height <= 0) return;

        _frameW = Math.Max(100, width - 2 * Gap);
        _frameH = _frameW / _barAspect;
        if (_frameH > height - 2 * Gap) // a very tall bar shape in a short window: fit by height instead
        {
            _frameH = Math.Max(10, height - 2 * Gap);
            _frameW = _frameH * _barAspect;
        }
        _frameX = (width - _frameW) / 2;
        _frameY = (height - _frameH) / 2;

        var stripRect = new Rect(_frameX, _frameY, _frameW, _frameH);
        Shade.Data = new GeometryGroup
        {
            FillRule = FillRule.EvenOdd, // the outer rectangle minus the strip
            Children = { new RectangleGeometry(new Rect(0, 0, width, height)), new RectangleGeometry(stripRect) },
        };
        Place(BarTint, stripRect);
        Place(Frame, Rect.Inflate(stripRect, 2, 2));
        Canvas.SetLeft(FrameLabel, _frameX);
        Canvas.SetTop(FrameLabel, _frameY + _frameH + 8);

        ShowCrop();
    }

    /// <summary>Puts the picture where the current crop says, and updates the zoom controls.</summary>
    private void ShowCrop()
    {
        if (_image is null || _frameW <= 0) return;
        var (w, h) = (_image.PixelWidth, _image.PixelHeight);
        _crop = CropMath.Clamp(_crop, w, h, _frameW, _frameH);

        var region = CropMath.Region(_crop, w, h, _frameW, _frameH);
        Picture.Width = w * region.Scale;
        Picture.Height = h * region.Scale;
        Canvas.SetLeft(Picture, _frameX - region.X * region.Scale);
        Canvas.SetTop(Picture, _frameY - region.Y * region.Scale);

        _updatingSlider = true;
        ZoomSlider.Value = _crop.Zoom;
        _updatingSlider = false;
        ZoomText.Text = $"{_crop.Zoom * 100:0}%";
    }

    private static void Place(FrameworkElement element, Rect rect)
    {
        Canvas.SetLeft(element, rect.X);
        Canvas.SetTop(element, rect.Y);
        element.Width = rect.Width;
        element.Height = rect.Height;
    }

    // ----- Dragging -----

    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Viewport.Focus();
        if (_image is null) return;
        _dragFrom = e.GetPosition(Viewport);
        Viewport.CaptureMouse();
        e.Handled = true;
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragFrom is not { } from || _image is null) return;
        var to = e.GetPosition(Viewport);
        _crop = CropMath.Drag(_crop, to.X - from.X, to.Y - from.Y, _image.PixelWidth, _image.PixelHeight, _frameW, _frameH);
        _dragFrom = to;
        ShowCrop();
    }

    private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Viewport.ReleaseMouseCapture();

    private void Viewport_LostMouseCapture(object sender, MouseEventArgs e) => _dragFrom = null;

    // ----- Zooming -----

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mouse = e.GetPosition(Viewport);
        // Zoom around the mouse, measured from the middle of the strip.
        ZoomTo(_crop.Zoom * (e.Delta > 0 ? WheelStep : 1 / WheelStep), mouse.X - (_frameX + _frameW / 2), mouse.Y - (_frameY + _frameH / 2));
        e.Handled = true;
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_updatingSlider) ZoomTo(e.NewValue);
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomTo(_crop.Zoom * WheelStep);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomTo(_crop.Zoom / WheelStep);

    private void ZoomTo(double zoom, double anchorX = 0, double anchorY = 0)
    {
        if (_image is null || _frameW <= 0) return;
        _crop = CropMath.ZoomAt(_crop, zoom, anchorX, anchorY, _image.PixelWidth, _image.PixelHeight, _frameW, _frameH);
        ShowCrop();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _crop = ImageCrop.Default;
        ShowCrop();
    }

    // ----- Keyboard -----

    private void Viewport_KeyDown(object sender, KeyEventArgs e)
    {
        if (_image is null) return;
        (double dx, double dy)? move = e.Key switch
        {
            Key.Left => (-KeyStep, 0),
            Key.Right => (KeyStep, 0),
            Key.Up => (0, -KeyStep),
            Key.Down => (0, KeyStep),
            _ => null,
        };

        if (move is { } m)
        {
            _crop = CropMath.Drag(_crop, m.dx, m.dy, _image.PixelWidth, _image.PixelHeight, _frameW, _frameH);
            ShowCrop();
        }
        else if (e.Key is Key.OemPlus or Key.Add)
        {
            ZoomTo(_crop.Zoom * WheelStep);
        }
        else if (e.Key is Key.OemMinus or Key.Subtract)
        {
            ZoomTo(_crop.Zoom / WheelStep);
        }
        else
        {
            return;
        }
        e.Handled = true;
    }

    private void Use_Click(object sender, RoutedEventArgs e)
    {
        Result = ThemeLibrary.Clean(_crop);
        DialogResult = true;
    }
}
