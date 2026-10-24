using System.Globalization;

namespace FocusBar.Core.Appearance;

/// <summary>An opaque color.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
}

/// <summary>Parsing "#RRGGBB" colors and picking readable text colors for them (WCAG contrast).</summary>
public static class ColorMath
{
    /// <summary>Accepts "#RGB", "#RRGGBB", with or without the "#", any case, surrounding spaces ignored.</summary>
    public static bool TryParse(string? text, out Rgb color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var hex = text.Trim();
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => $"{c}{c}"));
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        color = new Rgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }

    /// <summary>The color as "#RRGGBB", or null if it isn't one.</summary>
    public static string? Normalize(string? text) => TryParse(text, out var color) ? color.ToHex() : null;

    /// <summary>WCAG relative luminance, 0 (black) to 1 (white).</summary>
    public static double Luminance(Rgb color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    /// <summary>WCAG contrast ratio, 1 (none) to 21 (black on white).</summary>
    public static double Contrast(Rgb a, Rgb b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>True if white text reads better on this color than black text.</summary>
    public static bool IsDark(Rgb color) => Contrast(color, White) >= Contrast(color, Black);

    public static readonly Rgb White = new(255, 255, 255);
    public static readonly Rgb Black = new(0, 0, 0);
}
