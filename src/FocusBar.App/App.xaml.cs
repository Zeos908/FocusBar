using System.Windows;
using System.Windows.Threading;
using FocusBar.App.Safety;
using FocusBar.App.Shell;
using FocusBar.Core.Diagnostics;
using ManagedShell;
using ManagedShell.AppBar;
using ManagedShell.Common.Helpers;
using ManagedShell.Interop;

namespace FocusBar.App;

public partial class App : Application
{
    private readonly TaskbarGuard _guard;
    private readonly ErrorLog _log;

    private ShellManager? _shell;
    private PanicHotkey? _panicHotkey;
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

        EnvironmentHelper.IsAppRunningAsShell = NativeMethods.GetShellWindow() == IntPtr.Zero;
        _shell = new ShellManager(ShellConfiguration());
        _guard.StopHiding = () => _shell.ExplorerHelper.HideExplorerTaskbar = false;
        _shell.ExplorerHelper.HideExplorerTaskbar = true;

        _panicHotkey = new PanicHotkey(() => RestoreTaskbarAndExit($"{PanicHotkey.Description} pressed"), _log);

        OpenBar();
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
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        Cleanup($"Windows {e.ReasonSessionEnding.ToString().ToLowerInvariant()}");
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

    private static ShellConfig ShellConfiguration()
    {
        var config = ShellManager.DefaultShellConfig;
        // Phase 1 has no tray area yet, so leave tray icons with Explorer until Phase 2 displays them.
        config.AutoStartTrayService = false;
        return config;
    }

    private void OpenBar()
    {
        _bar = new MainBar(this, _shell!, AppBarScreen.FromPrimaryScreen());
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
    private void Cleanup(string reason)
    {
        if (_cleanedUp) return;
        _cleanedUp = true;

        Try(() => _panicHotkey?.Dispose(), "dispose panic hotkey");
        Try(CloseBar, "close bar");
        Try(() => { if (_shell is not null) _shell.ExplorerHelper.HideExplorerTaskbar = false; }, "stop hiding taskbar");
        Try(() => _shell?.Dispose(), "dispose ManagedShell");
        _guard.RestoreAndForget(reason);
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
