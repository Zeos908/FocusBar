namespace FocusBar.Core.Settings;

/// <summary>
/// "Start with Windows": a value named <c>FocusBar</c> under the current user's
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> key. FocusBar only writes it when the student turns the
/// setting on, and removes it when they turn it off (the uninstaller removes it too).
/// </summary>
public static class StartupCommand
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Where Task Manager's Startup apps page records that an entry was turned off.</summary>
    public const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public const string ValueName = "FocusBar";

    /// <summary>The command Windows runs at sign-in: the quoted path to FocusBar.exe (quotes keep spaces safe).</summary>
    public static string For(string exePath) => $"\"{exePath.Trim().Trim('"')}\"";

    /// <summary>The program a Run command starts: the quoted part, or everything up to ".exe" if it isn't quoted.</summary>
    public static string? ProgramPath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var text = command.Trim();

        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }

        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0 ? text[..(exe + 4)] : text;
    }

    /// <summary>True if the Run value starts this copy of FocusBar (Windows paths ignore case).</summary>
    public static bool PointsTo(string? command, string exePath) =>
        ProgramPath(command) is { } program && program.Equals(exePath.Trim().Trim('"'), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True if Task Manager's Startup apps page has the entry turned off. Windows stores a binary value whose first
    /// byte is even (2, 6) for on and odd (3, 7) for off; a missing value means on.
    /// </summary>
    public static bool IsTurnedOffInTaskManager(byte[]? approved) => approved is { Length: > 0 } && (approved[0] & 1) == 1;
}
