using System.Windows;
using FocusBar.App.Safety;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Safety;
using FocusBar.Core.Storage;

namespace FocusBar.App;

internal static class Program
{
    public const string RestoreTaskbarArgument = "--restore-taskbar";
    public const string RestartedArgument = "--restarted";

    private const string SingleInstanceMutexName = @"Local\FocusBar.SingleInstance";

    [STAThread]
    public static int Main(string[] args)
    {
        var paths = AppDataPaths.Default;
        var log = new ErrorLog(paths);
        var safetyStore = new TaskbarSafetyStore(new JsonFileStore(paths, log));

        var mode = args.Length > 0 ? args[0] : null;
        if (mode == Watchdog.Argument) return Watchdog.Run(args, safetyStore, log);
        if (mode == RestoreTaskbarArgument) return RestoreTaskbarCommand(safetyStore, log);

        using var mutex = new Mutex(initiallyOwned: false, SingleInstanceMutexName);
        // After "Restart FocusBar" the old process may take a moment to exit, so wait for it.
        if (!TryAcquire(mutex, mode == RestartedArgument ? TimeSpan.FromSeconds(10) : TimeSpan.Zero))
        {
            MessageBox.Show("FocusBar is already running.", "FocusBar", MessageBoxButton.OK, MessageBoxImage.Information);
            return 1;
        }

        try
        {
            var guard = new TaskbarGuard(safetyStore, log);

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                log.Write(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()),
                          "Unhandled exception; FocusBar is closing.");
                guard.EmergencyRestore("unhandled exception");
            };

            // In .NET 8 an unobserved task exception does not end the process, so the bar keeps running
            // and the taskbar stays replaced. Log it so it can be fixed, and mark it observed.
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                log.Write(e.Exception, "Unobserved exception in a background task (FocusBar kept running).");
                e.SetObserved();
            };

            var app = new App(guard, log);
            app.InitializeComponent();
            return app.Run();
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    /// <summary>
    /// Relaunches FocusBar (used after a crash when the user picks "Restart").
    /// </summary>
    public static void StartNewInstance(ErrorLog log)
    {
        try
        {
            SelfLauncher.Start(RestartedArgument);
        }
        catch (Exception ex)
        {
            log.Write(ex, "Could not restart FocusBar.");
        }
    }

    private static bool TryAcquire(Mutex mutex, TimeSpan wait)
    {
        try
        {
            return mutex.WaitOne(wait);
        }
        catch (AbandonedMutexException)
        {
            // The previous FocusBar was killed while holding it; we own it now.
            return true;
        }
    }

    /// <summary>
    /// <c>FocusBar.exe --restore-taskbar</c>: a manual escape hatch if FocusBar and its watchdog were both killed.
    /// </summary>
    private static int RestoreTaskbarCommand(TaskbarSafetyStore store, ErrorLog log)
    {
        var state = store.Load();
        if (state is not null && ProcessInfo.IsRunning(state.OwnerProcessId, state.OwnerStartTime))
        {
            MessageBox.Show(
                $"FocusBar is still running. Press {PanicHotkey.Description}, or right-click the bar and choose " +
                "\"Restore Windows taskbar and exit\".",
                "FocusBar", MessageBoxButton.OK, MessageBoxImage.Information);
            return 1;
        }

        var original = state?.OriginalAppBarState ?? TaskbarRestorer.GetState() ?? TaskbarRestorer.DefaultAppBarState;
        var found = TaskbarRestorer.Restore(original);
        store.Clear();
        log.Info($"--restore-taskbar run; Explorer taskbar {(found ? "restored" : "not found")}.");

        MessageBox.Show(
            found
                ? "The Windows taskbar has been restored."
                : "Couldn't find the Windows taskbar. Open Task Manager (Ctrl+Shift+Esc), choose \"Run new task\", and run explorer.exe.",
            "FocusBar", MessageBoxButton.OK, found ? MessageBoxImage.Information : MessageBoxImage.Warning);
        return found ? 0 : 1;
    }
}
