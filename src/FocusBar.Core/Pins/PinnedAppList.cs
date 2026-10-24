namespace FocusBar.Core.Pins;

/// <summary>
/// The ordered list of pinned apps. Pure logic; <see cref="PinStore"/> saves it.
/// </summary>
public sealed class PinnedAppList
{
    private readonly List<PinnedApp> _apps;

    public PinnedAppList(IEnumerable<PinnedApp>? apps = null)
    {
        _apps = [];
        foreach (var app in apps ?? []) Add(app);
    }

    public IReadOnlyList<PinnedApp> Apps => _apps;

    public bool Contains(string path) => Find(path) is not null;

    public PinnedApp? Find(string path) => _apps.FirstOrDefault(app => app.Matches(path));

    /// <summary>Adds to the end. Returns false (and changes nothing) if it is already pinned.</summary>
    public bool Add(PinnedApp app)
    {
        if (string.IsNullOrWhiteSpace(app.Path) || _apps.Any(existing => existing.Matches(app))) return false;
        _apps.Add(app);
        return true;
    }

    /// <summary>Removes whichever pin matches <paramref name="path"/> (its own path or its shortcut target).</summary>
    public bool Remove(string path)
    {
        var app = Find(path);
        return app is not null && _apps.Remove(app);
    }

    /// <summary>Moves a pin left (negative) or right (positive). Returns false if it can't move that way.</summary>
    public bool Move(string path, int offset)
    {
        var app = Find(path);
        if (app is null) return false;

        var from = _apps.IndexOf(app);
        var to = Math.Clamp(from + offset, 0, _apps.Count - 1);
        if (to == from) return false;

        _apps.RemoveAt(from);
        _apps.Insert(to, app);
        return true;
    }
}
