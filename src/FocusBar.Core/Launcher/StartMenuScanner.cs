namespace FocusBar.Core.Launcher;

/// <summary>
/// Finds installed apps by looking for shortcuts in the Start Menu folders, the same place the
/// Windows Start menu gets its "All apps" list (apart from Microsoft Store apps).
/// </summary>
public static class StartMenuScanner
{
    /// <summary>Shortcut types worth listing: regular shortcuts, web shortcuts, and ClickOnce apps.</summary>
    private static readonly string[] ShortcutExtensions = [".lnk", ".url", ".appref-ms"];

    /// <summary>
    /// The current user's Start Menu first, then the one shared by all users. When both have a shortcut with
    /// the same name, the current user's wins.
    /// </summary>
    public static IReadOnlyList<string> DefaultFolders() =>
    [
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),        // %AppData%\Microsoft\Windows\Start Menu
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),  // %ProgramData%\Microsoft\Windows\Start Menu
    ];

    /// <summary>
    /// Lists shortcuts under <paramref name="folders"/> (searching subfolders), sorted by name.
    /// Skips uninstallers and duplicates. Folders that are missing or unreadable are skipped, never thrown.
    /// </summary>
    public static IReadOnlyList<AppEntry> Scan(IEnumerable<string> folders)
    {
        var byName = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folders)
        {
            foreach (var path in SafeEnumerate(folder))
            {
                if (!ShortcutExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) continue;

                var name = Path.GetFileNameWithoutExtension(path).Trim();
                if (name.Length == 0 || IsClutter(name)) continue;

                byName.TryAdd(name, new AppEntry(name, path));
            }
        }

        return [.. byName.Values.OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Uninstallers clutter the list and are one wrong Enter away from removing an app.</summary>
    private static bool IsClutter(string name) =>
        name.Contains("uninstall", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SafeEnumerate(string folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return [];

        try
        {
            return Directory.EnumerateFiles(folder, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System,
            }).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
