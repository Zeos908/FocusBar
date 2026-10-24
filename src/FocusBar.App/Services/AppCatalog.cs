using FocusBar.Core.Diagnostics;
using FocusBar.Core.Launcher;

namespace FocusBar.App.Services;

/// <summary>
/// The list of installed apps for the Start launcher. Scans the Start Menu folders in the background
/// and keeps the last result, so the launcher opens instantly and refreshes while it's open.
/// </summary>
internal sealed class AppCatalog
{
    private readonly ErrorLog _log;
    private Task<IReadOnlyList<AppEntry>>? _scan;

    public AppCatalog(ErrorLog log)
    {
        _log = log;
    }

    /// <summary>The most recent scan result (empty before the first scan finishes).</summary>
    public IReadOnlyList<AppEntry> Latest { get; private set; } = [];

    /// <summary>Rescans unless a scan is already running. Never throws.</summary>
    public Task<IReadOnlyList<AppEntry>> RefreshAsync()
    {
        if (_scan is { IsCompleted: false }) return _scan;

        _scan = Task.Run(() =>
        {
            try
            {
                var apps = StartMenuScanner.Scan(StartMenuScanner.DefaultFolders());
                Latest = apps;
                return apps;
            }
            catch (Exception ex)
            {
                _log.Write(ex, "Could not scan the Start Menu folders.");
                return Latest;
            }
        });
        return _scan;
    }
}
