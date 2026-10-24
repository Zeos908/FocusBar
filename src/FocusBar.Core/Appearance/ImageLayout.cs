using FocusBar.Core.Models;

namespace FocusBar.Core.Appearance;

/// <summary>
/// How to load and place an image so it's drawn one image pixel per screen pixel when it's being scaled down (sharp
/// at any DPI) and only the part that shows is kept in memory.
/// </summary>
/// <param name="DecodeWidth">Decode (scale) the image to this many pixels wide...</param>
/// <param name="DecodeHeight">...and this many high. Never bigger than the image itself.</param>
/// <param name="CropX">Then keep this region of the decoded pixels (what's visible).</param>
/// <param name="CropY"></param>
/// <param name="CropWidth"></param>
/// <param name="CropHeight"></param>
/// <param name="DisplayWidth">Draw it this big in device-independent units, centered. That's the kept pixels divided
/// by the DPI scale, or bigger when a small image (or a zoomed-in part) has to be scaled up.</param>
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
    /// <param name="crop">For Fill: the part chosen in the picture editor (null = the middle, not zoomed).</param>
    public static ImagePlan Plan(int imageWidth, int imageHeight, double targetWidth, double targetHeight, double dpiScale,
        ImageFit fit, ImageCrop? crop = null)
    {
        if (imageWidth <= 0 || imageHeight <= 0) throw new ArgumentOutOfRangeException(nameof(imageWidth), "The image has no pixels.");
        if (dpiScale <= 0 || double.IsNaN(dpiScale)) dpiScale = 1;

        // The target in real screen pixels; at least 1 so the math never divides by zero.
        var targetPxW = Math.Max(1, (int)Math.Round(targetWidth * dpiScale));
        var targetPxH = Math.Max(1, (int)Math.Round(targetHeight * dpiScale));

        // The part of the image to show (in image pixels), and screen pixels per image pixel.
        double x, y, width, height, scale;
        switch (fit)
        {
            case ImageFit.Fill:
                (x, y, width, height, scale) = CropMath.Region(crop ?? ImageCrop.Default, imageWidth, imageHeight, targetPxW, targetPxH);
                break;
            case ImageFit.Fit:
                scale = Math.Min((double)targetPxW / imageWidth, (double)targetPxH / imageHeight);
                (x, y, width, height) = (0, 0, imageWidth, imageHeight);
                break;
            default: // Center: real size, cut to the target
                scale = 1;
                (width, height) = (Math.Min(imageWidth, targetPxW), Math.Min(imageHeight, targetPxH));
                (x, y) = ((imageWidth - width) / 2, (imageHeight - height) / 2);
                break;
        }

        // Scaling down happens while decoding (high quality, and nothing extra in memory). Scaling up happens when
        // drawing, so a zoomed-in or small picture never needs a huge decode.
        var decodeScale = Math.Min(Math.Min(scale, 1.0), (double)MaxDecodeSide / Math.Max(imageWidth, imageHeight));
        var decodeW = Math.Max(1, (int)Math.Round(imageWidth * decodeScale));
        var decodeH = Math.Max(1, (int)Math.Round(imageHeight * decodeScale));

        var cropX = Math.Clamp((int)Math.Round(x * decodeScale), 0, decodeW - 1);
        var cropY = Math.Clamp((int)Math.Round(y * decodeScale), 0, decodeH - 1);
        var cropW = Math.Clamp((int)Math.Round(width * decodeScale), 1, decodeW - cropX);
        var cropH = Math.Clamp((int)Math.Round(height * decodeScale), 1, decodeH - cropY);

        return new ImagePlan(decodeW, decodeH, cropX, cropY, cropW, cropH, width * scale / dpiScale, height * scale / dpiScale);
    }
}
