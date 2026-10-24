using FocusBar.Core.Models;

namespace FocusBar.Core.Appearance;

/// <summary>A rectangle of the picture, in picture pixels, and how many target units each picture pixel covers.</summary>
public readonly record struct CropRegion(double X, double Y, double Width, double Height, double Scale);

/// <summary>
/// The math behind the bar picture editor and <see cref="ImageLayout"/>: which part of a picture fills a target
/// (the bar, or the bar-shaped frame in the editor) for an <see cref="ImageCrop"/>, and how dragging and zooming
/// change it. The picture always covers the whole target; it can't be dragged or zoomed out past its edges.
/// </summary>
public static class CropMath
{
    /// <summary>Target units per picture pixel when the picture just covers the target.</summary>
    public static double CoverScale(int imageWidth, int imageHeight, double targetWidth, double targetHeight) =>
        Math.Max(targetWidth / imageWidth, targetHeight / imageHeight);

    /// <summary>Fixes impossible values and moves the center so the shown region stays inside the picture.</summary>
    public static ImageCrop Clamp(ImageCrop crop, int imageWidth, int imageHeight, double targetWidth, double targetHeight)
    {
        var zoom = double.IsFinite(crop.Zoom) ? Math.Clamp(crop.Zoom, 1, ImageCrop.MaxZoom) : 1;
        var scale = CoverScale(imageWidth, imageHeight, targetWidth, targetHeight) * zoom;

        // Half the shown region, as a fraction of the picture's width and height.
        var halfWidth = targetWidth / scale / imageWidth / 2;
        var halfHeight = targetHeight / scale / imageHeight / 2;

        return new ImageCrop(Keep(crop.CenterX, halfWidth), Keep(crop.CenterY, halfHeight), zoom);

        static double Keep(double center, double half) =>
            half >= 0.5 ? 0.5 : Math.Clamp(double.IsFinite(center) ? center : 0.5, half, 1 - half);
    }

    /// <summary>The part of the picture that fills the target.</summary>
    public static CropRegion Region(ImageCrop crop, int imageWidth, int imageHeight, double targetWidth, double targetHeight)
    {
        var clamped = Clamp(crop, imageWidth, imageHeight, targetWidth, targetHeight);
        var scale = CoverScale(imageWidth, imageHeight, targetWidth, targetHeight) * clamped.Zoom;
        var width = targetWidth / scale;
        var height = targetHeight / scale;
        return new CropRegion(clamped.CenterX * imageWidth - width / 2, clamped.CenterY * imageHeight - height / 2, width, height, scale);
    }

    /// <summary>The picture dragged by (<paramref name="dx"/>, <paramref name="dy"/>) target units.</summary>
    public static ImageCrop Drag(ImageCrop crop, double dx, double dy, int imageWidth, int imageHeight, double targetWidth, double targetHeight)
    {
        var current = Clamp(crop, imageWidth, imageHeight, targetWidth, targetHeight);
        var scale = CoverScale(imageWidth, imageHeight, targetWidth, targetHeight) * current.Zoom;
        // Dragging the picture right shows more of its left side, so the center moves the other way.
        var moved = current with
        {
            CenterX = current.CenterX - dx / scale / imageWidth,
            CenterY = current.CenterY - dy / scale / imageHeight,
        };
        return Clamp(moved, imageWidth, imageHeight, targetWidth, targetHeight);
    }

    /// <summary>
    /// Zooms to <paramref name="zoom"/>, keeping the picture point under the anchor in place (the anchor is measured
    /// from the middle of the target, so (0, 0) zooms around the middle; the mouse position zooms around the mouse).
    /// </summary>
    public static ImageCrop ZoomAt(ImageCrop crop, double zoom, double anchorX, double anchorY,
        int imageWidth, int imageHeight, double targetWidth, double targetHeight)
    {
        var current = Clamp(crop, imageWidth, imageHeight, targetWidth, targetHeight);
        var cover = CoverScale(imageWidth, imageHeight, targetWidth, targetHeight);
        var newZoom = double.IsFinite(zoom) ? Math.Clamp(zoom, 1, ImageCrop.MaxZoom) : current.Zoom;
        var (oldScale, newScale) = (cover * current.Zoom, cover * newZoom);

        // The picture point (in picture pixels) under the anchor, before and after.
        var pointX = current.CenterX * imageWidth + anchorX / oldScale;
        var pointY = current.CenterY * imageHeight + anchorY / oldScale;
        var zoomed = new ImageCrop((pointX - anchorX / newScale) / imageWidth, (pointY - anchorY / newScale) / imageHeight, newZoom);
        return Clamp(zoomed, imageWidth, imageHeight, targetWidth, targetHeight);
    }
}
