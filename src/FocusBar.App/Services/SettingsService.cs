using FocusBar.Core.Diagnostics;
using FocusBar.Core.Settings;

namespace FocusBar.App.Services;

/// <summary>The current settings, saved to <c>settings.json</c> whenever they change.</summary>
internal sealed class SettingsService
{
    private readonly SettingsStore _store;
    private readonly ErrorLog _log;

    public SettingsService(SettingsStore store, ErrorLog log)
    {
        _store = store;
        _log = log;
        Current = store.Load();
    }

    public event EventHandler? Changed;

    public AppSettings Current { get; private set; }

    public void Update(Func<AppSettings, AppSettings> change)
    {
        Current = change(Current);
        try
        {
            _store.Save(Current);
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not save settings.json.");
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
