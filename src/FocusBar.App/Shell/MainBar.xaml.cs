using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FocusBar.App.Flyouts;
using FocusBar.App.Services;
using FocusBar.App.Themes;
using FocusBar.App.Tray;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Models;
using FocusBar.Core.Storage;
using ManagedShell;
using ManagedShell.AppBar;
using ManagedShell.Common.Helpers;
using ManagedShell.Interop;
using ManagedShell.WindowsTray;

namespace FocusBar.App.Shell;

/// <summary>Everything the bar needs that outlives a single bar window (it's recreated when Explorer restarts).</summary>
internal sealed record BarServices(
    ShellManager Shell,
    PinService Pins,
    AppCatalog Apps,
    StatusIndicators Status,
    TrayIcons? TrayIcons,
    ErrorLog Log);

/// <summary>
/// The bar itself: an AppBar docked to the bottom of the primary monitor. Registering as an AppBar
/// reserves screen space, so maximized windows stop above it just like they do for the real taskbar.
/// </summary>
public partial class MainBar : AppBarWindow
{
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int WM_POWERBROADCAST = 0x0218;
    private static readonly int TaskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");

    private readonly App _app;
    private readonly BarServices _services;
    private readonly DispatcherTimer _clockTimer = new(DispatcherPriority.Background);
    private readonly FlyoutToggle<StartLauncher> _launcher;
    private readonly FlyoutToggle<CalendarFlyout> _calendar;

    internal MainBar(App app, BarServices services, AppBarScreen screen)
        : base(services.Shell.AppBarManager, services.Shell.ExplorerHelper, services.Shell.FullScreenHelper,
               screen, AppBarEdge.Bottom, AppBarMode.Normal, Theme.DefaultBarHeight)
    {
        _app = app;
        _services = services;
        InitializeComponent();

#if DEBUG
        DeveloperMenu.Visibility = Visibility.Visible;
#endif

        _launcher = new FlyoutToggle<StartLauncher>(() => new StartLauncher(services.Apps, services.Pins, services.Log));
        _calendar = new FlyoutToggle<CalendarFlyout>(() => new CalendarFlyout(services.Log));

        Pinned.Initialize(services.Pins, services.Log);
        Tasks.Initialize(services.Shell, services.Pins);
        Tray.Initialize(services.Status, services.TrayIcons);
        TrayIconView.BeforeClick = UpdateTrayHost;

        _clockTimer.Tick += (_, _) => UpdateClock();
        UpdateClock();
        _clockTimer.Start();

        LocationChanged += (_, _) => UpdateTrayHost();
        SizeChanged += (_, _) => UpdateTrayHost();
    }

    /// <summary>
    /// Updates the time, then sleeps until the next minute starts so the clock costs almost nothing.
    /// </summary>
    private void UpdateClock()
    {
        var now = DateTime.Now;
        TimeText.Text = now.ToString("t");
        DateText.Text = now.ToString("d");
        ClockButton.ToolTip = now.ToString("D");
        _clockTimer.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond);
    }

    /// <summary>
    /// Tells apps where the tray is (in device pixels), so the menus they open from their tray icons
    /// appear next to the bar. Follows RetroBar's Taskbar.SetTrayHost.
    /// </summary>
    private void UpdateTrayHost()
    {
        if (_services.Shell.NotificationArea is not { IsFailed: false } area || !IsLoaded) return;

        area.SetTrayHostSizeData(new TrayHostSizeData
        {
            edge = (NativeMethods.ABEdge)AppBarEdge,
            rc = new NativeMethods.Rect
            {
                Top = (int)(Top * DpiScale),
                Left = (int)(Left * DpiScale),
                Bottom = (int)((Top + ActualHeight) * DpiScale),
                Right = (int)((Left + ActualWidth) * DpiScale),
            },
        });
    }

    protected override void OnSourceInitialized(object sender, EventArgs e)
    {
        base.OnSourceInitialized(sender, e);
        UpdateTrayHost();
    }

    protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == TaskbarCreatedMessage)
        {
            // Explorer restarted. Let this message finish before replacing the window.
            Dispatcher.BeginInvoke(_app.OnExplorerTaskbarCreated);
        }
        else if (msg == WM_SETTINGCHANGE && lParam != IntPtr.Zero && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet")
        {
            // Windows switched between light and dark mode.
            Dispatcher.BeginInvoke(ThemeManager.Apply);
        }
        else if (msg == WM_POWERBROADCAST)
        {
            Dispatcher.BeginInvoke(_services.Status.RefreshBattery);
        }

        return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
    }

    protected override void CustomClosing()
    {
        if (!AllowClose) return;
        _clockTimer.Stop();
        _launcher.Close();
        _calendar.Close();
        Tray.CloseFlyouts();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e) => _launcher.Toggle(StartButton, HorizontalAlignment.Left);

    /// <summary>Right-clicking Start opens Windows' own power-user menu (the same as Win+X).</summary>
    private void StartButton_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true;
        ShellHelper.ShowStartContextMenu();
    }

    private void ClockButton_Click(object sender, RoutedEventArgs e) => _calendar.Toggle(ClockButton, HorizontalAlignment.Right);

    private void TaskManager_Click(object sender, RoutedEventArgs e) => ShellHelper.StartTaskManager();

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
