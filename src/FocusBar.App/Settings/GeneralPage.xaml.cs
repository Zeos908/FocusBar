using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using FocusBar.App.Services;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Storage;

namespace FocusBar.App.Settings;

/// <summary>Settings → General: Start with Windows, the setup guide, the way out, and where FocusBar keeps its files.</summary>
public partial class GeneralPage : UserControl
{
    private readonly ErrorLog _log;
    private readonly Action _runSetup;
    private bool _loading = true;

    internal GeneralPage(ErrorLog log, Action runSetup)
    {
        _log = log;
        _runSetup = runSetup;
        InitializeComponent();

        StartupBox.IsChecked = StartWithWindows.IsOn();
        UpdateStartupNote();

        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        VersionText.Text = $"FocusBar {version.Split('+')[0]}";
        _loading = false;
    }

    private void StartupBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var on = StartupBox.IsChecked == true;
        if (!StartWithWindows.Set(on, _log))
        {
            _loading = true;
            StartupBox.IsChecked = !on;
            _loading = false;
        }
        UpdateStartupNote();
    }

    private void UpdateStartupNote()
    {
        StartupNote.Text = StartupBox.IsChecked == true && StartWithWindows.IsTurnedOffInTaskManager()
            ? "Windows has FocusBar turned off in Task Manager → Startup apps, so it won't start until you turn it on there too."
            : "Off unless you turn it on. You can also change it in Task Manager → Startup apps.";
    }

    private void RunSetup_Click(object sender, RoutedEventArgs e) => _runSetup();

    private void OpenData_Click(object sender, RoutedEventArgs e) => OpenFolder(AppDataPaths.Default.Root);

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenFolder(AppDataPaths.Default.Logs);

    private void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _log.Write(ex, $"Could not open {folder}.");
        }
    }
}
