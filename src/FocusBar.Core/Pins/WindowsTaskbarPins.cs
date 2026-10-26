namespace FocusBar.Core.Pins;

/// <summary>A shortcut pinned to the Windows taskbar.</summary>
/// <param name="ShortcutPath">The .lnk file in Windows' pinned-apps folder.</param>
/// <param name="TargetPath">The program it opens, if Windows could tell (used to match open windows).</param>
/// <param name="Created">When the shortcut file was made, which is roughly when it was pinned.</param>
public sealed record WindowsPin(string ShortcutPath, string? TargetPath, DateTime Created);

/// <summary>
/// Brings over the apps pinned to the Windows taskbar, so FocusBar starts with the same pins instead of an empty bar.
/// Windows keeps desktop-app pins as shortcuts in <see cref="DefaultFolder"/>; FocusBar copies them into its own
/// folder, so unpinning something from the (hidden) Windows taskbar later can't break FocusBar's pins.
/// </summary>
public static class WindowsTaskbarPins
{
    /// <summary><c>%AppData%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar</c></summary>
    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");

    /// <summary>
    /// Which shortcuts to pin and in what order (oldest first, the order they were pinned in as far as the files can
    /// tell), each pointing at FocusBar's copy in <paramref name="copyFolder"/>. Skips anything that isn't a shortcut,
    /// FocusBar itself, and the same program pinned twice.
    /// </summary>
    public static IReadOnlyList<(WindowsPin Source, PinnedApp Pin)> Plan(IEnumerable<WindowsPin> shortcuts, string copyFolder)
    {
        var pins = new PinnedAppList();
        var plan = new List<(WindowsPin, PinnedApp)>();

        foreach (var shortcut in shortcuts
                     .Where(s => s.ShortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(s => s.Created)
                     .ThenBy(s => FileName(s.ShortcutPath), StringComparer.OrdinalIgnoreCase))
        {
            if (IsFocusBar(shortcut.TargetPath)) continue;

            var fileName = FileName(shortcut.ShortcutPath);
            var target = string.IsNullOrWhiteSpace(shortcut.TargetPath) ? null : shortcut.TargetPath;
            var pin = new PinnedApp(Path.Combine(copyFolder, fileName), fileName[..^".lnk".Length], target);
            if (pins.Add(pin)) plan.Add((shortcut, pin));
        }
        return plan;
    }

    /// <summary>The file name of a Windows path (either slash), whatever system the code runs on.</summary>
    private static string FileName(string path) => path[(path.LastIndexOfAny(['\\', '/']) + 1)..];

    private static bool IsFocusBar(string? target) =>
        target is not null && FileName(target).Equals("FocusBar.exe", StringComparison.OrdinalIgnoreCase);
}
