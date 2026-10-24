namespace FocusBar.Core.Models;

/// <summary>
/// Which apps' tray icons are shown on the bar (the rest are under the ^ button). Saved to <c>tray.json</c>.
/// </summary>
/// <param name="PinnedIcons">
/// ManagedShell icon identifiers: the icon's GUID if it has one, otherwise <c>"path\to\app.exe:ID"</c>.
/// </param>
public record TraySettings(List<string> PinnedIcons)
{
    public const string FileName = "tray.json";

    /// <summary>New icons start hidden under the ^ button, like on Windows 11.</summary>
    public static TraySettings Default => new([]);
}
