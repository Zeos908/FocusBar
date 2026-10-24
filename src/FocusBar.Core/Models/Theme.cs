namespace FocusBar.Core.Models;

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

    public static Theme Default => new(
        Name: "Default",
        BackgroundImagePath: null,
        StartIconPath: null,
        FocusScreenImagePath: null,
        AccentColor: "#4F8EF7",
        Opacity: 1.0,
        BarHeight: DefaultBarHeight);
}
