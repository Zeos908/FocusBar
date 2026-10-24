using System.Windows;
using System.Windows.Threading;
using FocusBar.App.Deadlines;
using FocusBar.App.Focus;
using FocusBar.App.Safety;
using FocusBar.App.Security;
using FocusBar.App.Services;
using FocusBar.App.Setup;
using FocusBar.App.Shell;
using FocusBar.App.Themes;
using FocusBar.App.Tray;
using FocusBar.Core.Appearance;
using FocusBar.Core.Deadlines;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Focus;
using FocusBar.Core.Pins;
using FocusBar.Core.Reminders;
using FocusBar.Core.Settings;
using FocusBar.Core.Sources;
using FocusBar.Core.Sources.School;
using FocusBar.Core.Storage;
using ManagedShell;
using ManagedShell.AppBar;
using ManagedShell.Common.Enums;
using ManagedShell.Common.Helpers;
using ManagedShell.Interop;

namespace FocusBar.App;

public partial class App : Application
{
    private readonly TaskbarGuard _guard;
    private readonly ErrorLog _log;

    private ShellManager? _shell;
    private PanicHotkey? _panicHotkey;
    private StatusIndicators? _status;
    private ReminderService? _reminders;
    private SyncService? _sync;
    private FocusController? _focus;
    private AppearanceService? _appearance;
    private BarServices? _services;
    private MainBar? _bar;
    private bool _crashing;
    private bool _cleanedUp;

    internal App(TaskbarGuard guard, ErrorLog log)
    {
        _guard = guard;
        _log = log;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _log.Info("FocusBar starting.");

        // Safety net first: undo anything a previous run left behind, and record the taskbar's
        // state before touching it so every exit path can put it back.
        _guard.RecoverFromPreviousRun();
        _guard.RecordBeforeHiding();
        Watchdog.Launch(_log);

        var store = new JsonFileStore(AppDataPaths.Default, _log);

        EnvironmentHelper.IsAppRunningAsShell = NativeMethods.GetShellWindow() == IntPtr.Zero;
        _shell = new ShellManager(ShellConfiguration(store));
        _guard.StopHiding = () => _shell.ExplorerHelper.HideExplorerTaskbar = false;
        _shell.ExplorerHelper.HideExplorerTaskbar = true;

        _panicHotkey = new PanicHotkey(() => RestoreTaskbarAndExit($"{PanicHotkey.Description} pressed"), _log);

        ThemeManager.Apply();
        _services = CreateServices(_shell, store);
        OpenBar();
        _focus!.Start();

        // First run: the setup guide, once the bar is up (so the student sees what's being set up).
        if (!_services.Settings.Current.SetupCompleted)
        {
            Dispatcher.BeginInvoke(() => { if (!_cleanedUp) SetupWizard.ShowSingle(_services); }, DispatcherPriority.ApplicationIdle);
        }
    }

    /// <summary>Used by the panic hotkey and the "Restore Windows taskbar and exit" menu item.</summary>
    internal void RestoreTaskbarAndExit(string reason)
    {
        _log.Info($"Exit requested: {reason}.");
        Cleanup(reason);
        Shutdown();
    }

    /// <summary>
    /// Explorer recreated its taskbar (for example after explorer.exe restarted). Our AppBar registration
    /// was lost with it, so open a fresh bar. ManagedShell re-hides Explorer's taskbar on its own.
    /// </summary>
    internal void OnExplorerTaskbarCreated()
    {
        if (_cleanedUp) return;
        _log.Info("Explorer restarted; reopening the bar.");
        CloseBar();
        OpenBar();

        // Start the window list over so it doesn't keep File Explorer windows that closed with Explorer
        // (RetroBar does the same).
        Try(() =>
        {
            _shell!.Tasks.Dispose();
            _shell.Tasks.Initialize(true);
        }, "restart window list after Explorer restarted");
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        Cleanup($"Windows {e.ReasonSessionEnding.ToString().ToLowerInvariant()}", sessionEnding: true);
        // If another app cancels the sign-out, FocusBar has already given the taskbar back, so finish exiting
        // rather than lingering with no bar. The user can start it again.
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cleanup("app exit");
        _log.Info("FocusBar exited.");
        base.OnExit(e);
    }

    private static ShellConfig ShellConfiguration(JsonFileStore store)
    {
        var config = ShellManager.DefaultShellConfig;
        // 32 px icons look sharp at 24 px on the bar at 100% and still decent at 150%.
        config.TaskIconSize = IconSize.Large;
        config.PinnedNotifyIcons = TrayIcons.LoadPinned(store);
        return config;
    }

    private BarServices CreateServices(ShellManager shell, JsonFileStore store)
    {
        _status = new StatusIndicators(Dispatcher, _log);

        TrayIcons? trayIcons = null;
        if (shell.NotificationArea is { } area)
        {
            if (area.IsFailed) _log.Warning("The tray service failed to start; other apps' tray icons won't show on the bar.");
            trayIcons = new TrayIcons(area, store, _log);
        }

        var apps = new AppCatalog(_log);
        _ = apps.RefreshAsync(); // so the Start launcher's first open is instant

        var deadlines = new DeadlineService(new DeadlineStore(store), new ScheduleStore(store), _log);
        var settings = new SettingsService(new SettingsStore(store), _log);

        var protector = new DpapiProtector();
        _sync = new SyncService(deadlines, new SchoolApiConfigStore(store), new SchoolSessionStore(store, protector),
            new CalendarFeedStore(store, protector), _log, Dispatcher);

        var sync = _sync;
        _reminders = new ReminderService(deadlines, settings, new ReminderStateStore(store), _log, Dispatcher)
        {
            OpenList = () => DeadlineListWindow.ShowSingle(deadlines, sync),
        };

        var appearance = _appearance = new AppearanceService(new ThemeStore(store), new ThemeImages(AppDataPaths.Default), _log, Dispatcher);
        ThemeManager.ApplyAccent(appearance.Theme.AccentColor);
        appearance.Changed += OnAppearanceChanged;

        _focus = new FocusController(settings, deadlines, new FocusStateStore(store),
            () => appearance.ImagePath(appearance.Theme.FocusScreenImagePath), _log, Dispatcher);

        return new BarServices(shell, new PinService(new PinStore(store), _log), apps, _status, trayIcons, deadlines,
            settings, _reminders, _sync, _focus, appearance, _log);
    }

    /// <summary>
    /// The accent color is used by every window, so it's set app-wide. Switching between a see-through and a solid
    /// bar needs a new bar window; everything else the bar applies itself.
    /// </summary>
    private void OnAppearanceChanged(object? sender, EventArgs e)
    {
        if (_cleanedUp || _appearance is null) return;
        ThemeManager.ApplyAccent(_appearance.Theme.AccentColor);
        if (_bar is not null && _bar.NeedsNewWindow(_appearance.Theme))
        {
            _log.Info("Theme switched between see-through and solid; reopening the bar.");
            CloseBar();
            OpenBar();
        }
    }

    private void OpenBar()
    {
        _bar = new MainBar(this, _services!, AppBarScreen.FromPrimaryScreen());
        _bar.Show();
    }

    private void CloseBar()
    {
        if (_bar is null) return;
        _bar.AllowClose = true;
        _bar.Close();
        _bar = null;
    }

    /// <summary>
    /// Restores the Windows taskbar. Runs exactly once, on whichever exit path gets here first.
    /// Each step is isolated so one failure can't stop the taskbar from coming back.
    /// </summary>
    private void Cleanup(string reason, bool sessionEnding = false)
    {
        if (_cleanedUp) return;
        _cleanedUp = true;

        Try(() => _panicHotkey?.Dispose(), "dispose panic hotkey");
        Try(() => _focus?.Dispose(), "stop Focus Mode");
        Try(() => _appearance?.Flush(), "save theme");
        Try(CloseBar, "close bar");
        Try(() => _status?.Dispose(), "stop Wi-Fi, volume, and battery monitors");
        Try(() => _reminders?.Dispose(), "stop reminders");
        Try(() => _sync?.Dispose(), "stop deadline sync");
        Try(() => { if (_shell is not null) _shell.ExplorerHelper.HideExplorerTaskbar = false; }, "stop hiding taskbar");
        Try(() => _shell?.Dispose(), "dispose ManagedShell");
        _guard.RestoreAndForget(reason);

        // Last, because it can wait on Windows' permission prompt and the taskbar must already be back. Skipped while
        // Windows is signing out or shutting down (a prompt then would hold it up); the next start removes the block.
        if (sessionEnding)
        {
            if (WebsiteBlocker.HasBlock()) _log.Info("Leaving the website block for the next FocusBar start to remove (Windows is signing out).");
        }
        else
        {
            Try(() => _focus?.RemoveWebsiteBlockForExit(), "remove website block");
        }
    }

    private void Try(Action action, string what)
    {
        try { action(); }
        catch (Exception ex) { _log.Write(ex, $"Cleanup step failed: {what}."); }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        if (_crashing) return;
        _crashing = true;

        _log.Write(e.Exception, "Unhandled exception on the UI thread; FocusBar is closing.");
        _guard.EmergencyRestore("unhandled UI exception");

        var restart = MessageBox.Show(
            "FocusBar ran into a problem and needs to close. Your Windows taskbar is back.\n\n" +
            $"Details were saved to:\n{_log.CurrentLogFile}\n\nRestart FocusBar?",
            "FocusBar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

        Cleanup("unhandled UI exception");
        if (restart) Program.StartNewInstance(_log);
        Shutdown();
    }
}
