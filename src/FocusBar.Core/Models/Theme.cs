namespace FocusBar.Core.Models;

/// <summary>How the bar's background image is sized.</summary>
public enum ImageFit
{
    /// <summary>Covers the whole bar, cropping what doesn't fit (the usual choice for a wide, thin bar).</summary>
    Fill,

    /// <summary>The whole image is visible, with empty space at the sides.</summary>
    Fit,

    /// <summary>The image at its real size, in the middle of the bar.</summary>
    Center,
}

/// <summary>
/// How the bar looks. The image paths are file names inside <c>%AppData%\FocusBar\images</c>
/// (see <see cref="Appearance.ThemeImages"/>), so the student's originals can move or be deleted.
/// The positional properties are the ones from the build instructions; the rest were added in Phase 7.
/// </summary>
public record Theme(
    string Name,
    string? BackgroundImagePath,
    string? StartIconPath,
    string? FocusScreenImagePath,
    string AccentColor,
    double Opacity,
    int BarHeight)
{
    public const int DefaultBarHeight = 48;
    public const int MinBarHeight = 32;
    public const int MaxBarHeight = 72;
    public const double MinOpacity = 0.4;
    public const string DefaultAccent = "#4F8EF7";
    public const string DefaultFont = "Segoe UI Variable Text";

    /// <summary>Fill, Fit, or Center for <see cref="BackgroundImagePath"/>.</summary>
    public ImageFit BackgroundFit { get; init; } = ImageFit.Fill;

    /// <summary>The bar's color as "#RRGGBB", or null to follow Windows' light or dark mode.</summary>
    public string? BarColor { get; init; }

    /// <summary>The font for the bar (the clock, window buttons, and ticker).</summary>
    public string FontFamily { get; init; } = DefaultFont;

    public static Theme Default => new(
        Name: "Default",
        BackgroundImagePath: null,
        StartIconPath: null,
        FocusScreenImagePath: null,
        AccentColor: DefaultAccent,
        Opacity: 1.0,
        BarHeight: DefaultBarHeight);
}
