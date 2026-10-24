using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FocusBar.Core.Appearance;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Models;

namespace FocusBar.App.Themes;

/// <summary>A decoded image ready to draw: show it at exactly <see cref="Width"/> × <see cref="Height"/>.</summary>
internal sealed record LoadedImage(BitmapSource Bitmap, double Width, double Height);

/// <summary>A whole picture for the editor, and its real (upright) size in pixels.</summary>
internal sealed record PreviewImage(BitmapSource Bitmap, int PixelWidth, int PixelHeight);

/// <summary>
/// Loads the student's images so they're sharp at any display scaling: each is scaled once, with high quality,
/// to the exact number of screen pixels it will cover (see <see cref="ImageLayout"/>), and only the visible part is
/// kept. Safe to call from a background thread; the result is frozen.
/// </summary>
internal static class ThemeImageLoader
{
    public static LoadedImage? Load(string path, double width, double height, double dpiScale, ImageFit fit, ErrorLog log,
        ImageCrop? crop = null)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);

            // Size and camera rotation, without decoding the pixels.
            var (pixelWidth, pixelHeight, rotation) = Probe(bytes);
            var sideways = rotation is 90 or 270;
            var (shownWidth, shownHeight) = sideways ? (pixelHeight, pixelWidth) : (pixelWidth, pixelHeight);

            var plan = ImageLayout.Plan(shownWidth, shownHeight, width, height, dpiScale, fit, crop);

            // Decode straight to the planned size (WIC scales down with high quality while decoding).
            var decoded = new BitmapImage();
            decoded.BeginInit();
            decoded.StreamSource = new MemoryStream(bytes, writable: false);
            decoded.CacheOption = BitmapCacheOption.OnLoad;
            decoded.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            decoded.DecodePixelWidth = sideways ? plan.DecodeHeight : plan.DecodeWidth;
            decoded.DecodePixelHeight = sideways ? plan.DecodeWidth : plan.DecodeHeight;
            decoded.EndInit();

            BitmapSource source = decoded;
            if (rotation != 0) source = new TransformedBitmap(source, new RotateTransform(rotation)); // exact for 90° steps

            // Copy out just the visible part, so the full-size decode can be freed.
            var visible = new Int32Rect(plan.CropX, plan.CropY,
                Math.Min(plan.CropWidth, source.PixelWidth - plan.CropX), Math.Min(plan.CropHeight, source.PixelHeight - plan.CropY));
            var pixels = new FormatConvertedBitmap(new CroppedBitmap(source, visible), PixelFormats.Pbgra32, null, 0);
            var stride = visible.Width * 4;
            var buffer = new byte[stride * visible.Height];
            pixels.CopyPixels(buffer, stride, 0);

            var result = BitmapSource.Create(visible.Width, visible.Height, 96, 96, PixelFormats.Pbgra32, null, buffer, stride);
            result.Freeze();
            return new LoadedImage(result, plan.DisplayWidth, plan.DisplayHeight);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException
                                       or InvalidOperationException or COMException or FileFormatException or OverflowException)
        {
            log.Write(ex, $"Could not load the image {Path.GetFileName(path)}.");
            return null;
        }
    }

    /// <summary>
    /// The whole picture, the right way up, at most <paramref name="maxSide"/> pixels on its longer side (for the
    /// picture editor). <see cref="PreviewImage.PixelWidth"/> and <see cref="PreviewImage.PixelHeight"/> are the
    /// picture's real size, which is what crops are measured against.
    /// </summary>
    public static PreviewImage? LoadPreview(string path, int maxSide, ErrorLog log)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var (pixelWidth, pixelHeight, rotation) = Probe(bytes);
            var sideways = rotation is 90 or 270;
            var scale = Math.Min(1.0, (double)maxSide / Math.Max(pixelWidth, pixelHeight));

            var decoded = new BitmapImage();
            decoded.BeginInit();
            decoded.StreamSource = new MemoryStream(bytes, writable: false);
            decoded.CacheOption = BitmapCacheOption.OnLoad;
            decoded.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            decoded.DecodePixelWidth = Math.Max(1, (int)Math.Round(pixelWidth * scale));
            decoded.DecodePixelHeight = Math.Max(1, (int)Math.Round(pixelHeight * scale));
            decoded.EndInit();

            BitmapSource bitmap = rotation == 0 ? decoded : new TransformedBitmap(decoded, new RotateTransform(rotation));
            bitmap.Freeze();
            return sideways ? new PreviewImage(bitmap, pixelHeight, pixelWidth) : new PreviewImage(bitmap, pixelWidth, pixelHeight);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException
                                       or InvalidOperationException or COMException or FileFormatException or OverflowException)
        {
            log.Write(ex, $"Could not open the image {Path.GetFileName(path)} for editing.");
            return null;
        }
    }

    /// <summary>Pixel size and the clockwise rotation a phone camera asked for (EXIF orientation 3, 6, 8).</summary>
    private static (int Width, int Height, int Rotation) Probe(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);

        var rotation = 0;
        try
        {
            if (frame.Metadata is BitmapMetadata metadata && metadata.GetQuery("System.Photo.Orientation") is ushort orientation)
            {
                rotation = orientation switch { 3 => 180, 6 => 90, 8 => 270, _ => 0 };
            }
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException or COMException)
        {
            // PNG and GIF have no camera rotation; some files have unreadable metadata.
        }
        return (frame.PixelWidth, frame.PixelHeight, rotation);
    }
}
