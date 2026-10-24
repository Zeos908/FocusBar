using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Pins;
using ManagedShell.ShellFolders;
using ManagedShell.WindowsTasks;

namespace FocusBar.App.Services;

/// <summary>
/// The pinned apps shown on the bar. Wraps the testable <see cref="PinnedAppList"/>, keeps the bar's
/// <see cref="Items"/> in step with it, and saves every change to <c>pins.json</c>.
/// </summary>
internal sealed class PinService
{
    private readonly PinnedAppList _list;
    private readonly PinStore _store;
    private readonly ErrorLog _log;

    public PinService(PinStore store, ErrorLog log)
    {
        _store = store;
        _log = log;
        _list = store.Load();
        Items = new ObservableCollection<PinnedAppItem>(_list.Apps.Select(app => new PinnedAppItem(app)));
    }

    public ObservableCollection<PinnedAppItem> Items { get; }

    public bool IsPinned(string path) => _list.Contains(path);

    public void Pin(PinnedApp app)
    {
        if (!_list.Add(app)) return;
        Items.Add(new PinnedAppItem(app));
        Save();
    }

    public void Unpin(string path)
    {
        var app = _list.Find(path);
        if (app is null || !_list.Remove(path)) return;

        var item = Items.FirstOrDefault(i => i.App == app);
        if (item is not null) Items.Remove(item);
        Save();
    }

    public void Move(string path, int offset)
    {
        var app = _list.Find(path);
        if (app is null || !_list.Move(path, offset)) return;

        var item = Items.First(i => i.App == app);
        Items.Move(Items.IndexOf(item), _list.Apps.ToList().IndexOf(app));
        Save();
    }

    /// <summary>A pin for a Start Menu shortcut, remembering the program it opens.</summary>
    public PinnedApp ForShortcut(string shortcutPath, string name)
    {
        string? target = null;
        if (shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                target = ShellLinkHelper.GetLinkTarget(IntPtr.Zero, shortcutPath);
            }
            catch (Exception ex)
            {
                _log.Write(ex, $"Could not read shortcut {shortcutPath}; pinning it without its target.");
            }
        }

        return new PinnedApp(shortcutPath, name, string.IsNullOrWhiteSpace(target) ? null : target);
    }

    /// <summary>A pin for the program behind a running window, or null if Windows won't say what it is.</summary>
    public static PinnedApp? ForWindow(ApplicationWindow window)
    {
        if (window.IsUWP && !string.IsNullOrEmpty(window.AppUserModelID))
        {
            return new PinnedApp(PinnedApp.AppxPrefix + window.AppUserModelID, window.Title);
        }

        var exe = window.WinFileName;
        if (string.IsNullOrWhiteSpace(exe)) return null;

        var name = !string.IsNullOrWhiteSpace(window.WinFileDescription)
            ? window.WinFileDescription
            : Path.GetFileNameWithoutExtension(exe);
        return new PinnedApp(exe, name);
    }

    /// <summary>The path a running window is known by, for checking whether it's pinned.</summary>
    public static string? PathOf(ApplicationWindow window) =>
        window.IsUWP && !string.IsNullOrEmpty(window.AppUserModelID)
            ? PinnedApp.AppxPrefix + window.AppUserModelID
            : window.WinFileName;

    private void Save()
    {
        try
        {
            _store.Save(_list);
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not save pins.json; pin changes will be lost when FocusBar closes.");
        }
    }
}

/// <summary>One pinned app button on the bar.</summary>
internal sealed class PinnedAppItem : ObservableObject
{
    private bool _iconRequested;
    private ImageSource? _icon;

    public PinnedAppItem(PinnedApp app)
    {
        App = app;
    }

    public PinnedApp App { get; }
    public string Name => App.Name;
    public string Path => App.Path;

    /// <summary>Loads in the background the first time something displays it.</summary>
    public ImageSource? Icon
    {
        get
        {
            if (!_iconRequested) LoadIcon();
            return _icon;
        }
        private set => SetProperty(ref _icon, value);
    }

    private async void LoadIcon()
    {
        _iconRequested = true;
        Icon = await IconLoader.LoadAsync(Path);
    }
}
