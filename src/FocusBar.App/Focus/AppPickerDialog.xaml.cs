using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using FocusBar.App.Native;
using FocusBar.App.Services;
using FocusBar.App.Themes;
using FocusBar.Core.Focus;
using FocusBar.Core.Launcher;
using ManagedShell.ShellFolders;

namespace FocusBar.App.Focus;

/// <summary>
/// Picks an app for the block list: apps open right now (most useful: open the game, then pick it) and apps
/// installed in the Start Menu. Returns the program's process name, which is what Focus Mode watches for.
/// </summary>
public partial class AppPickerDialog : Window
{
    private const string Running = "Open now";
    private const string Installed = "Installed";

    private readonly List<PickerItem> _items;

    internal AppPickerDialog(AppCatalog catalog, Window owner)
    {
        Owner = owner;
        InitializeComponent();

        _items = [.. RunningApps(), .. catalog.Latest.Select(app => new PickerItem(Installed, app.Name, "Start Menu app", null, app))];
        var view = new ListCollectionView(_items);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PickerItem.Group)));
        AppList.ItemsSource = view;

        SourceInitialized += (_, _) => DarkTitleBar.Apply(this, !ThemeManager.IsLight);
        Loaded += (_, _) => SearchBox.Focus();
    }

    /// <summary>The chosen program's process name, e.g. "Discord".</summary>
    public string? ChosenProcessName { get; private set; }

    /// <summary>Programs with a visible window, one entry per program, skipping Windows' own.</summary>
    private static IEnumerable<PickerItem> RunningApps()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<PickerItem>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(process.MainWindowTitle)) continue;
                    var name = process.ProcessName;
                    if (BlockList.IsProtected(name) || !seen.Add(name)) continue;
                    items.Add(new PickerItem(Running, process.MainWindowTitle, $"{name}.exe", name, null));
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    // Exited or not accessible.
                }
            }
        }
        return items.OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        ((ListCollectionView)AppList.ItemsSource).Filter = query.Length == 0
            ? null
            : item => item is PickerItem p && (p.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                                               p.Detail.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    private void AppList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Choose();

    private void Choose_Click(object sender, RoutedEventArgs e) => Choose();

    private void Choose()
    {
        if (AppList.SelectedItem is not PickerItem item)
        {
            ShowError("Pick an app from the list.");
            return;
        }

        var name = item.ProcessName ?? ProcessNameOf(item.Shortcut!);
        if (name is null)
        {
            ShowError($"FocusBar can't tell which program \"{item.Title}\" runs. Open it, then pick it under \"{Running}\".");
            return;
        }
        if (BlockList.IsProtected(name))
        {
            ShowError($"{name} is part of Windows or FocusBar and can't be blocked.");
            return;
        }

        ChosenProcessName = name;
        DialogResult = true;
    }

    /// <summary>The .exe a Start Menu shortcut opens, or null (for example a Store app or a web link).</summary>
    private static string? ProcessNameOf(AppEntry app)
    {
        if (!app.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            var target = ShellLinkHelper.GetLinkTarget(IntPtr.Zero, app.Path);
            return target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? BlockList.NormalizeAppName(Path.GetFileName(target)) : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    /// <summary>One row: an open app (with its process name) or a Start Menu app (resolved when chosen).</summary>
    private sealed record PickerItem(string Group, string Title, string Detail, string? ProcessName, AppEntry? Shortcut);
}
