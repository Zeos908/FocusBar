using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusBar.App.Services;
using FocusBar.Core.Pins;
using ManagedShell;
using ManagedShell.WindowsTasks;

namespace FocusBar.App.Shell;

/// <summary>
/// One app button on the bar: a pinned app, open or not, or an open app that isn't pinned. All of an app's windows
/// share the button (see <see cref="TaskbarButtons"/>).
/// </summary>
internal sealed class AppButton : ObservableObject
{
    private PinnedAppItem? _pin;
    private IReadOnlyList<ApplicationWindow> _windows = [];
    private ImageSource? _icon;
    private string _name = "";
    private string _tooltip = "";
    private string _state = "Closed";

    public AppButton(string key)
    {
        Key = key;
    }

    public string Key { get; }

    public PinnedAppItem? Pin => _pin;

    public IReadOnlyList<ApplicationWindow> Windows
    {
        get => _windows;
        private set => SetProperty(ref _windows, value);
    }

    public int Count => Windows.Count;

    /// <summary>Shows the number of windows on the button when there's more than one.</summary>
    public bool ShowCount => Count > 1;

    public string CountText => Count > 9 ? "9+" : Count.ToString();

    public ImageSource? Icon
    {
        get => _icon;
        private set => SetProperty(ref _icon, value);
    }

    /// <summary>The app's name ("Google Chrome").</summary>
    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    /// <summary>Tooltip and screen-reader name: the window's title, or "Google Chrome · 3 windows".</summary>
    public string Tooltip
    {
        get => _tooltip;
        private set => SetProperty(ref _tooltip, value);
    }

    /// <summary>Closed (pinned, not open), Open, Active (has the focused window), or Flashing (wants attention).</summary>
    public string State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    /// <summary>What "Open new window" starts: the pin, or the program behind the windows.</summary>
    public string? LaunchPath => Pin?.Path ?? (Windows.Count > 0 ? PinService.PathOf(Windows[0]) : null);

    internal void Update(PinnedAppItem? pin, IReadOnlyList<ApplicationWindow> windows)
    {
        if (!ReferenceEquals(pin, _pin))
        {
            if (_pin is not null) _pin.PropertyChanged -= OnPinChanged;
            _pin = pin;
            if (_pin is not null) _pin.PropertyChanged += OnPinChanged;
            OnPropertyChanged(nameof(Pin));
        }

        var countChanged = windows.Count != Windows.Count;
        Windows = windows;
        if (countChanged)
        {
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(ShowCount));
            OnPropertyChanged(nameof(CountText));
        }

        State = windows.Count == 0 ? "Closed"
            : windows.Any(w => w.State == ApplicationWindow.WindowState.Flashing) ? "Flashing"
            : windows.Any(w => w.State == ApplicationWindow.WindowState.Active) ? "Active"
            : "Open";
        Icon = pin?.Icon ?? windows.Select(w => w.Icon).FirstOrDefault(i => i is not null);
        Name = pin?.Name ?? AppName(windows[0]);
        Tooltip = windows.Count switch
        {
            0 => Name,
            1 => string.IsNullOrWhiteSpace(windows[0].Title) ? Name : windows[0].Title,
            _ => $"{Name} · {windows.Count} windows",
        };
    }

    internal void Detach()
    {
        if (_pin is not null) _pin.PropertyChanged -= OnPinChanged;
    }

    private void OnPinChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PinnedAppItem.Icon)) Icon = _pin?.Icon ?? Windows.Select(w => w.Icon).FirstOrDefault(i => i is not null);
    }

    private static string AppName(ApplicationWindow window)
    {
        if (!string.IsNullOrWhiteSpace(window.WinFileDescription)) return window.WinFileDescription;
        if (!window.IsUWP && !string.IsNullOrWhiteSpace(window.WinFileName)) return Path.GetFileNameWithoutExtension(window.WinFileName);
        return string.IsNullOrWhiteSpace(window.Title) ? "Window" : window.Title;
    }
}

/// <summary>
/// Keeps the bar's app buttons in step with the pinned apps and the open windows. Any change (a window opening,
/// closing, getting focus, or changing its title or icon; a pin added, removed, or moved) rebuilds the list once,
/// updating existing buttons in place so the bar doesn't flicker.
/// </summary>
internal sealed class AppButtons : IDisposable
{
    private readonly ShellManager _shell;
    private readonly PinService _pins;
    private readonly Dispatcher _dispatcher;
    private readonly HashSet<ApplicationWindow> _watched = [];
    private bool _rebuildQueued;
    private bool _disposed;

    public AppButtons(ShellManager shell, PinService pins, Dispatcher dispatcher)
    {
        _shell = shell;
        _pins = pins;
        _dispatcher = dispatcher;

        ((INotifyCollectionChanged)shell.Tasks.GroupedWindows).CollectionChanged += OnSourceChanged;
        pins.Items.CollectionChanged += OnSourceChanged;
        Rebuild();
    }

    public ObservableCollection<AppButton> Items { get; } = [];

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueRebuild();

    /// <summary>Window changes the buttons show or group by (progress bars and overlay badges aren't shown).</summary>
    private static readonly HashSet<string> WatchedProperties =
    [
        nameof(ApplicationWindow.State), nameof(ApplicationWindow.Title), nameof(ApplicationWindow.Icon),
        nameof(ApplicationWindow.WinFileName), nameof(ApplicationWindow.WinFileDescription),
        nameof(ApplicationWindow.AppUserModelID), nameof(ApplicationWindow.IsUWP),
    ];

    private void OnWindowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || WatchedProperties.Contains(e.PropertyName)) QueueRebuild();
    }

    /// <summary>Many changes often arrive together (a window opening sets its title, icon, and state); rebuild once.</summary>
    private void QueueRebuild()
    {
        if (_rebuildQueued || _disposed) return;
        _rebuildQueued = true;
        _dispatcher.BeginInvoke(Rebuild, DispatcherPriority.Normal);
    }

    private void Rebuild()
    {
        _rebuildQueued = false;
        if (_disposed) return;

        var windows = _shell.Tasks.GroupedWindows.Cast<ApplicationWindow>().ToList();
        WatchOnly(windows);

        var pinItems = _pins.Items.ToDictionary(item => item.App);
        var buttons = TaskbarButtons.Build([.. _pins.Items.Select(item => item.App)], windows, PinService.PathOf,
            window => window.Handle.ToString());

        for (var i = 0; i < buttons.Count; i++)
        {
            var spec = buttons[i];
            var existing = IndexOf(spec.Key, from: i);
            if (existing < 0) Items.Insert(i, new AppButton(spec.Key));
            else if (existing != i) Items.Move(existing, i);

            Items[i].Update(spec.Pin is null ? null : pinItems[spec.Pin], spec.Windows);
        }

        while (Items.Count > buttons.Count)
        {
            Items[^1].Detach();
            Items.RemoveAt(Items.Count - 1);
        }
    }

    private int IndexOf(string key, int from)
    {
        for (var i = from; i < Items.Count; i++)
        {
            if (Items[i].Key == key) return i;
        }
        return -1;
    }

    /// <summary>Listens to exactly the windows on the bar (for focus, title, and icon changes).</summary>
    private void WatchOnly(List<ApplicationWindow> windows)
    {
        var current = windows.ToHashSet();
        foreach (var gone in _watched.Where(w => !current.Contains(w)).ToList())
        {
            gone.PropertyChanged -= OnWindowChanged;
            _watched.Remove(gone);
        }
        foreach (var window in current.Where(_watched.Add))
        {
            window.PropertyChanged += OnWindowChanged;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        ((INotifyCollectionChanged)_shell.Tasks.GroupedWindows).CollectionChanged -= OnSourceChanged;
        _pins.Items.CollectionChanged -= OnSourceChanged;
        foreach (var window in _watched) window.PropertyChanged -= OnWindowChanged;
        _watched.Clear();
        foreach (var button in Items) button.Detach();
    }
}
