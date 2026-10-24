using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using FocusBar.Core.Models;
using FocusBar.Core.Storage;
using ManagedShell;
using ManagedShell.AppBar;
using ManagedShell.Interop;

namespace FocusBar.App.Shell;

/// <summary>
/// The bar itself: an AppBar docked to the bottom of the primary monitor. Registering as an AppBar
/// reserves screen space, so maximized windows stop above it just like they do for the real taskbar.
/// </summary>
public partial class MainBar : AppBarWindow
{
    private static readonly int TaskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");

    private readonly App _app;
    private readonly DispatcherTimer _clockTimer = new(DispatcherPriority.Background);

    internal MainBar(App app, ShellManager shell, AppBarScreen screen)
        : base(shell.AppBarManager, shell.ExplorerHelper, shell.FullScreenHelper, screen, AppBarEdge.Bottom, AppBarMode.Normal, Theme.DefaultBarHeight)
    {
        _app = app;
        InitializeComponent();

#if DEBUG
        DeveloperMenu.Visibility = Visibility.Visible;
#endif

        _clockTimer.Tick += (_, _) => UpdateClock();
        UpdateClock();
        _clockTimer.Start();
    }

    /// <summary>
    /// Updates the time, then sleeps until the next minute starts so the clock costs almost nothing.
    /// </summary>
    private void UpdateClock()
    {
        var now = DateTime.Now;
        TimeText.Text = now.ToString("t");
        DateText.Text = now.ToString("d");
        _clockTimer.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond);
    }

    protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == TaskbarCreatedMessage)
        {
            // Explorer restarted. Let this message finish before replacing the window.
            Dispatcher.BeginInvoke(_app.OnExplorerTaskbarCreated);
        }

        return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
    }

    protected override void CustomClosing()
    {
        if (AllowClose) _clockTimer.Stop();
    }

    private void RestoreAndExit_Click(object sender, RoutedEventArgs e) =>
        _app.RestoreTaskbarAndExit("menu: Restore Windows taskbar and exit");

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = AppDataPaths.Default.Logs;
        System.IO.Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true })?.Dispose();
    }

    // Developer-only (Debug builds): these exist to test the Phase 0 safety net.
    private void ThrowUiException_Click(object sender, RoutedEventArgs e) =>
        throw new InvalidOperationException("Test exception thrown from the Developer menu (UI thread).");

    private void ThrowBackgroundException_Click(object sender, RoutedEventArgs e) =>
        new Thread(() => throw new InvalidOperationException("Test exception thrown from the Developer menu (background thread)."))
        {
            IsBackground = true,
        }.Start();
}
