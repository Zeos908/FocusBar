using FocusBar.Core.Models;

namespace FocusBar.Core.Appearance;

/// <summary>
/// How to load and place an image so it's drawn one image pixel per screen pixel (sharp at any DPI) and only the
/// part that shows is kept in memory.
/// </summary>
/// <param name="DecodeWidth">Decode (scale) the image to this many pixels wide...</param>
/// <param name="DecodeHeight">...and this many high.</param>
/// <param name="CropX">Then keep this region of the decoded pixels (what's visible).</param>
/// <param name="CropY"></param>
/// <param name="CropWidth"></param>
/// <param name="CropHeight"></param>
/// <param name="DisplayWidth">Draw it this big in device-independent units (CropWidth / DPI scale), centered.</param>
/// <param name="DisplayHeight"></param>
public readonly record struct ImagePlan(
    int DecodeWidth, int DecodeHeight,
    int CropX, int CropY, int CropWidth, int CropHeight,
    double DisplayWidth, double DisplayHeight);

public static class ImageLayout
{
    /// <summary>Never decode bigger than this on either side, whatever the screen.</summary>
    public const int MaxDecodeSide = 8192;

    /// <param name="imageWidth">The image's size in pixels (after any rotation from the camera).</param>
    /// <param name="imageHeight"></param>
    /// <param name="targetWidth">The area to fill, in device-independent units (WPF's 1/96 inch).</param>
    /// <param name="targetHeight"></param>
    /// <param name="dpiScale">1.0 at 100% scaling, 1.25 at 125%, 1.5 at 150%.</param>
    /// <param name="fit">Fill, Fit, or Center.</param>
    public static ImagePlan Plan(int imageWidth, int imageHeight, double targetWidth, double targetHeight, double dpiScale, ImageFit fit)
    {
        if (imageWidth <= 0 || imageHeight <= 0) throw new ArgumentOutOfRangeException(nameof(imageWidth), "The image has no pixels.");
        if (dpiScale <= 0 || double.IsNaN(dpiScale)) dpiScale = 1;

        // The target in real screen pixels; at least 1 so the math never divides by zero.
        var targetPxW = Math.Max(1, (int)Math.Round(targetWidth * dpiScale));
        var targetPxH = Math.Max(1, (int)Math.Round(targetHeight * dpiScale));

        var scale = fit switch
        {
            ImageFit.Fill => Math.Max((double)targetPxW / imageWidth, (double)targetPxH / imageHeight),
            ImageFit.Fit => Math.Min((double)targetPxW / imageWidth, (double)targetPxH / imageHeight),
            _ => 1.0, // Center: real size
        };
        scale = Math.Min(scale, (double)MaxDecodeSide / Math.Max(imageWidth, imageHeight));

        var decodeW = Math.Clamp((int)Math.Round(imageWidth * scale), 1, MaxDecodeSide);
        var decodeH = Math.Clamp((int)Math.Round(imageHeight * scale), 1, MaxDecodeSide);

        // Keep only what fits in the target, taken from the middle.
        var cropW = Math.Min(decodeW, targetPxW);
        var cropH = Math.Min(decodeH, targetPxH);
        var cropX = (decodeW - cropW) / 2;
        var cropY = (decodeH - cropH) / 2;

        return new ImagePlan(decodeW, decodeH, cropX, cropY, cropW, cropH, cropW / dpiScale, cropH / dpiScale);
    }
}
