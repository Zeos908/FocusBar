using System.ComponentModel;
using System.Windows.Data;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Models;
using FocusBar.Core.Storage;
using ManagedShell.WindowsTray;
using ManagedNotifyIcon = ManagedShell.WindowsTray.NotifyIcon;

namespace FocusBar.App.Tray;

/// <summary>
/// Other apps' tray icons, from ManagedShell's notification area: the ones pinned to the bar, and the full list
/// for the ^ flyout. Windows' own network, volume, and battery icons are left out because FocusBar shows its own.
/// </summary>
internal sealed class TrayIcons
{
    private static readonly HashSet<string> ReplacedSystemIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        NotificationArea.NETWORK_GUID,
        NotificationArea.POWER_GUID,
        NotificationArea.VOLUME_GUID,
    };

    private readonly NotificationArea _area;
    private readonly JsonFileStore _store;
    private readonly ErrorLog _log;

    public TrayIcons(NotificationArea area, JsonFileStore store, ErrorLog log)
    {
        _area = area;
        _store = store;
        _log = log;

        Pinned = CreateView(icon => icon.IsPinned);
        Pinned.SortDescriptions.Add(new SortDescription(nameof(ManagedNotifyIcon.PinOrder), ListSortDirection.Ascending));
        ((ICollectionViewLiveShaping)Pinned).IsLiveSorting = true;
        ((ICollectionViewLiveShaping)Pinned).LiveSortingProperties.Add(nameof(ManagedNotifyIcon.PinOrder));

        All = CreateView(_ => true);
    }

    /// <summary>The settings ManagedShell should start with: which icons are pinned.</summary>
    public static string[] LoadPinned(JsonFileStore store) =>
        [.. store.Load(TraySettings.FileName, TraySettings.Default).PinnedIcons];

    /// <summary>Icons shown directly on the bar.</summary>
    public ListCollectionView Pinned { get; }

    /// <summary>Every icon, for the ^ flyout where they can be pinned or unpinned.</summary>
    public ListCollectionView All { get; }

    public bool IsFailed => _area.IsFailed;

    public void TogglePinned(ManagedNotifyIcon icon)
    {
        if (icon.IsPinned) icon.Unpin(); else icon.Pin();

        try
        {
            _store.Save(TraySettings.FileName, new TraySettings([.. _area.PinnedNotifyIcons]));
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not save tray.json.");
        }
    }

    private ListCollectionView CreateView(Predicate<ManagedNotifyIcon> extra)
    {
        var view = new ListCollectionView(_area.TrayIcons)
        {
            Filter = item => item is ManagedNotifyIcon icon && !icon.IsHidden && !IsReplaced(icon) && extra(icon),
        };
        view.IsLiveFiltering = true;
        view.LiveFilteringProperties.Add(nameof(ManagedNotifyIcon.IsHidden));
        view.LiveFilteringProperties.Add(nameof(ManagedNotifyIcon.IsPinned));
        return view;
    }

    private static bool IsReplaced(ManagedNotifyIcon icon) =>
        icon.GUID != Guid.Empty && ReplacedSystemIcons.Contains(icon.GUID.ToString());
}
