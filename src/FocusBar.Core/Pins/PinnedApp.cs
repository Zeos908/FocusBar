namespace FocusBar.Core.Pins;

/// <summary>An app pinned to the bar.</summary>
/// <param name="Path">
/// What to launch: a Start Menu shortcut (<c>.lnk</c>), an <c>.exe</c>, or <c>appx:&lt;AppUserModelID&gt;</c> for Store apps.
/// </param>
/// <param name="Name">Shown in the tooltip.</param>
/// <param name="TargetPath">
/// For a shortcut, the program it points to. Lets a running window (which only knows its <c>.exe</c>)
/// recognize that it is already pinned through a shortcut.
/// </param>
public record PinnedApp(string Path, string Name, string? TargetPath = null)
{
    public const string AppxPrefix = "appx:";

    public bool Matches(string path) =>
        PathsEqual(Path, path) || (TargetPath is not null && PathsEqual(TargetPath, path));

    public bool Matches(PinnedApp other) =>
        Matches(other.Path) || (other.TargetPath is not null && Matches(other.TargetPath));

    /// <summary>Windows paths are case-insensitive.</summary>
    public static bool PathsEqual(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
