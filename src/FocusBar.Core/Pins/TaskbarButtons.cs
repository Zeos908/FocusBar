namespace FocusBar.Core.Pins;

/// <summary>One button in the bar's app area: a pinned app (open or not), or an app that's open but not pinned.</summary>
/// <param name="Key">Stays the same while the button exists, so the bar can update it in place.</param>
/// <param name="Pin">The pin, if the app is pinned.</param>
/// <param name="Windows">The app's open windows, in the order they were opened (empty for a pinned app that isn't open).</param>
public sealed record TaskbarButton<TWindow>(string Key, PinnedApp? Pin, IReadOnlyList<TWindow> Windows);

/// <summary>
/// Decides the app buttons the way the Windows taskbar does: one button per app, not one per window. Pinned apps come
/// first, in pin order, and collect their own windows. Other apps follow in the order they were opened, with every
/// window of the same program on one button. A window that doesn't say which program it belongs to gets its own button.
/// </summary>
public static class TaskbarButtons
{
    /// <param name="pins">The pinned apps, in order.</param>
    /// <param name="windows">The open windows, in the order the window list has them.</param>
    /// <param name="appPathOf">The program a window belongs to (an .exe path or <c>appx:</c> ID), or null if unknown.</param>
    /// <param name="windowIdOf">A unique, stable ID for a window (its handle), for windows with no program.</param>
    public static IReadOnlyList<TaskbarButton<TWindow>> Build<TWindow>(
        IReadOnlyList<PinnedApp> pins, IEnumerable<TWindow> windows, Func<TWindow, string?> appPathOf, Func<TWindow, string> windowIdOf)
    {
        var pinned = pins.Select(pin => (Pin: pin, Windows: new List<TWindow>())).ToList();
        var others = new List<(string Key, List<TWindow> Windows)>();
        var othersByKey = new Dictionary<string, List<TWindow>>(StringComparer.Ordinal);

        foreach (var window in windows)
        {
            var path = appPathOf(window);
            var pinIndex = string.IsNullOrWhiteSpace(path) ? -1 : pinned.FindIndex(p => p.Pin.Matches(path));
            if (pinIndex >= 0)
            {
                pinned[pinIndex].Windows.Add(window);
                continue;
            }

            var key = string.IsNullOrWhiteSpace(path) ? "window:" + windowIdOf(window) : "app:" + Normalize(path);
            if (!othersByKey.TryGetValue(key, out var group))
            {
                group = [];
                othersByKey[key] = group;
                others.Add((key, group));
            }
            group.Add(window);
        }

        return
        [
            .. pinned.Select(p => new TaskbarButton<TWindow>("pin:" + Normalize(p.Pin.Path), p.Pin, p.Windows)),
            .. others.Select(o => new TaskbarButton<TWindow>(o.Key, null, o.Windows)),
        ];
    }

    /// <summary>Windows paths ignore case.</summary>
    private static string Normalize(string path) => path.Trim().ToLowerInvariant();
}
