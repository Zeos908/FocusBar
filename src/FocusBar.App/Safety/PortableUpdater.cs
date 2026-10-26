using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using System.Windows;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Settings;
using FocusBar.Core.Updates;
using Microsoft.Win32;

namespace FocusBar.App.Safety;

/// <summary>
/// Makes "extract the new zip and run FocusBar.exe" update the FocusBar you already have instead of starting a second
/// copy. A FocusBar started from an extracted zip (it has the <see cref="PortableInstall.MarkerFileName"/> file next
/// to it) asks once, then closes the running FocusBar, copies itself into <see cref="PortableInstall.DefaultFolder"/>,
/// keeps the Start menu shortcut and "Start with Windows" pointing there, and starts the installed copy. Your
/// deadlines, settings, and pictures live in %AppData%\FocusBar and aren't touched.
/// </summary>
internal static class PortableUpdater
{
    /// <summary>A running FocusBar exits (giving the taskbar back) when this is signalled; see <see cref="ListenForExitRequest"/>.</summary>
    private const string ExitRequestName = @"Local\FocusBar.ExitForUpdate";

    private static readonly TimeSpan WaitForExit = TimeSpan.FromSeconds(15);

    /// <summary>The result of <see cref="Run"/>.</summary>
    public enum Outcome
    {
        /// <summary>The installed copy was started (or there was nothing to do); this process should exit.</summary>
        Done,

        /// <summary>The student chose to run this copy as it is.</summary>
        RunHere,
    }

    /// <summary>True when this FocusBar came from the zip and isn't the installed copy.</summary>
    public static bool ShouldOffer() =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, PortableInstall.MarkerFileName)) &&
        !PortableInstall.SameFolder(AppContext.BaseDirectory, PortableInstall.DefaultFolder);

    public static Outcome Run(ErrorLog log)
    {
        var folder = PortableInstall.DefaultFolder;
        var installedExe = Path.Combine(folder, "FocusBar.exe");

        // Installed for everyone with the installer: updating Program Files needs admin rights, so the installer does it.
        var allUsersExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FocusBar", "FocusBar.exe");
        if (File.Exists(allUsersExe) && !File.Exists(installedExe))
        {
            var runHere = MessageBox.Show(
                "FocusBar is installed for all users (in Program Files). To update that copy, run the new FocusBar installer.\n\n" +
                "Yes: run this downloaded copy for now.\nNo: close.",
                "FocusBar", MessageBoxButton.YesNo, MessageBoxImage.Information);
            return runHere == MessageBoxResult.Yes ? Outcome.RunHere : Outcome.Done;
        }

        var thisVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
        var installedVersion = VersionOf(installedExe);

        switch (PortableInstall.Decide(thisVersion, installedVersion))
        {
            case InstallAction.InstalledIsNewer:
                var startInstalled = MessageBox.Show(
                    $"A newer FocusBar ({Show(installedVersion!)}) is already installed than this download ({Show(thisVersion)}).\n\n" +
                    "Yes: start the installed one.\nNo: run this older copy anyway.",
                    "FocusBar", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (startInstalled != MessageBoxResult.Yes) return Outcome.RunHere;
                Start(installedExe, log);
                return Outcome.Done;

            case InstallAction.Update:
                var update = MessageBox.Show(
                    $"Update FocusBar from {Show(installedVersion!)} to {Show(thisVersion)}?\n\n" +
                    $"This replaces the FocusBar in {folder} and starts it. Your deadlines, settings, and pictures stay as they are. " +
                    "Afterwards you can delete the folder you just extracted.\n\n" +
                    "Yes: update.\nNo: just run this copy.",
                    "Update FocusBar", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (update != MessageBoxResult.Yes) return Outcome.RunHere;
                break;

            default:
                var install = MessageBox.Show(
                    $"Install FocusBar {Show(thisVersion)}?\n\n" +
                    $"It goes in {folder}, with a Start menu shortcut. Next time, extracting a new zip and running FocusBar.exe " +
                    "updates it there instead of making another copy. Afterwards you can delete the folder you just extracted.\n\n" +
                    "Yes: install.\nNo: just run this copy.",
                    "Install FocusBar", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (install != MessageBoxResult.Yes) return Outcome.RunHere;
                break;
        }

        if (!CloseRunningFocusBar(log)) return Outcome.Done;

        if (!CopyInto(folder, log))
        {
            MessageBox.Show(
                $"FocusBar couldn't finish copying into {folder}. Details are in the log.\n\n" +
                "Try again after restarting your PC, or run FocusBar.exe from the folder you extracted.",
                "FocusBar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return Outcome.Done;
        }

        CreateStartMenuShortcuts(installedExe, log);
        RepointStartWithWindows(installedExe, log);
        log.Info($"FocusBar {Show(thisVersion)} installed to {folder} from {AppContext.BaseDirectory}.");
        Start(installedExe, log);
        return Outcome.Done;
    }

    /// <summary>
    /// Call in the running app: when an update asks, exit the normal way (the Windows taskbar comes back). Older
    /// FocusBar versions don't listen, so the updater also asks the student to close them by hand.
    /// </summary>
    public static IDisposable? ListenForExitRequest(Action exit, ErrorLog log)
    {
        try
        {
            var request = new EventWaitHandle(false, EventResetMode.AutoReset, ExitRequestName);
            var registration = ThreadPool.RegisterWaitForSingleObject(request, (_, _) => exit(), null, Timeout.Infinite, executeOnlyOnce: true);
            return new Listener(request, registration);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            log.Write(ex, "Could not listen for update requests.");
            return null;
        }
    }

    private sealed class Listener(EventWaitHandle request, RegisteredWaitHandle registration) : IDisposable
    {
        public void Dispose()
        {
            registration.Unregister(null);
            request.Dispose();
        }
    }

    // ----- Steps -----

    /// <summary>Asks any running FocusBar (and its watchdog) to exit, and waits until their files are free.</summary>
    private static bool CloseRunningFocusBar(ErrorLog log)
    {
        if (!OthersRunning()) return true;

        if (EventWaitHandle.TryOpenExisting(ExitRequestName, out var request))
        {
            using (request) request.Set();
        }

        var deadline = DateTime.UtcNow + WaitForExit;
        while (OthersRunning())
        {
            if (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(250);
                continue;
            }

            // An older FocusBar that doesn't know about updates, or one that's stuck.
            var answer = MessageBox.Show(
                "FocusBar is still running. Please close it first: press Ctrl+Alt+Shift+F (or right-click the bar and choose " +
                "\"Restore Windows taskbar and exit\"), then click OK.",
                "Update FocusBar", MessageBoxButton.OKCancel, MessageBoxImage.Information);
            if (answer != MessageBoxResult.OK)
            {
                log.Info("Update cancelled: FocusBar was still running.");
                return false;
            }
            deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        }
        return true;
    }

    /// <summary>Other FocusBar processes (the bar, its watchdog, or the website blocker) that would keep files locked.</summary>
    private static bool OthersRunning()
    {
        var me = Environment.ProcessId;
        foreach (var name in new[] { "FocusBar", "FocusBar.Blocker" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    if (process.Id != me) return true;
                }
            }
        }
        return false;
    }

    /// <summary>Copies this folder's files over the installed ones (never deleting anything already there).</summary>
    private static bool CopyInto(string folder, ErrorLog log)
    {
        var source = AppContext.BaseDirectory;
        var files = PortableInstall.FilesToCopy(Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(source, path)));

        foreach (var relative in files)
        {
            var destination = Path.Combine(folder, relative);
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(Path.Combine(source, relative), destination, overwrite: true);
                    break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (attempt < 8)
                    {
                        Thread.Sleep(250 * attempt); // antivirus or a just-exited process may still hold the file
                        continue;
                    }
                    log.Write(ex, $"Could not copy {relative} into {folder}.");
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>Start menu → FocusBar, and the emergency "Restore Windows taskbar (FocusBar)".</summary>
    private static void CreateStartMenuShortcuts(string exe, ErrorLog log)
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        CreateShortcut(Path.Combine(programs, "FocusBar.lnk"), exe, "", "Start FocusBar", log);
        CreateShortcut(Path.Combine(programs, "Restore Windows taskbar (FocusBar).lnk"), exe, Program.RestoreTaskbarArgument,
            "Brings the normal Windows taskbar back if it ever goes missing", log);
    }

    private static void CreateShortcut(string path, string target, string arguments, string description, ErrorLog log)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell isn't available.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(path);
            shortcut.TargetPath = target;
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = Path.GetDirectoryName(target);
            shortcut.Description = description;
            shortcut.IconLocation = target + ",0";
            shortcut.Save();
        }
        catch (Exception ex)
        {
            log.Write(ex, $"Could not create the shortcut {path}.");
        }
    }

    /// <summary>If the student turned on "Start with Windows" for an older copy, keep it on for the installed one.</summary>
    private static void RepointStartWithWindows(string exe, ErrorLog log)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupCommand.RunKeyPath, writable: true);
            if (key?.GetValue(StartupCommand.ValueName) is not string command || StartupCommand.PointsTo(command, exe)) return;
            key.SetValue(StartupCommand.ValueName, StartupCommand.For(exe), RegistryValueKind.String);
            log.Info("Start with Windows now points at the installed FocusBar.");
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            log.Write(ex, "Could not update the Start with Windows entry.");
        }
    }

    private static void Start(string exe, ErrorLog log)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            log.Write(ex, $"Could not start {exe}.");
            MessageBox.Show($"FocusBar is installed but couldn't be started. Start it from the Start menu, or run:\n{exe}",
                "FocusBar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static Version? VersionOf(string exe)
    {
        try
        {
            return File.Exists(exe) && Version.TryParse(FileVersionInfo.GetVersionInfo(exe).FileVersion, out var version) ? version : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
        {
            return null;
        }
    }

    /// <summary>"0.9.1" rather than "0.9.1.0".</summary>
    private static string Show(Version v) => v.Build >= 0 ? $"{v.Major}.{v.Minor}.{v.Build}" : $"{v.Major}.{v.Minor}";
}
