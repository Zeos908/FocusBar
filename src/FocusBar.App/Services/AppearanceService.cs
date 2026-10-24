using System.IO;
using System.Windows.Threading;
using FocusBar.Core.Appearance;
using FocusBar.Core.Diagnostics;
using FocusBar.Core.Models;

namespace FocusBar.App.Services;

/// <summary>
/// The current theme and the student's saved themes. Every change shows on the bar right away (live preview);
/// saving to <c>themes.json</c> waits half a second so dragging a slider doesn't write the file dozens of times.
/// </summary>
internal sealed class AppearanceService
{
    private readonly ThemeStore _store;
    private readonly ThemeImages _images;
    private readonly ErrorLog _log;
    private readonly DispatcherTimer _saveTimer;

    public AppearanceService(ThemeStore store, ThemeImages images, ErrorLog log, Dispatcher dispatcher)
    {
        _store = store;
        _images = images;
        _log = log;
        Current = store.Load();

        _saveTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => Flush(), dispatcher);
        _saveTimer.Stop();
    }

    /// <summary>Raised on the UI thread after any change.</summary>
    public event EventHandler? Changed;

    public ThemeSettings Current { get; private set; }

    public Theme Theme => Current.Current;

    /// <summary>Changes the theme in use (it shows at once).</summary>
    public void UpdateTheme(Func<Theme, Theme> change) => Update(s => s with { Current = ThemeLibrary.Clean(change(s.Current)) });

    /// <summary>Applies, saves, or deletes themes (see <see cref="ThemeLibrary"/>).</summary>
    public void Update(Func<ThemeSettings, ThemeSettings> change)
    {
        var next = change(Current);
        if (next == Current) return;
        Current = next;
        _saveTimer.Stop();
        _saveTimer.Start();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Copies a picked image into FocusBar's image folder and returns its stored name.
    /// Throws <see cref="ThemeImageException"/> with a message for the student.
    /// </summary>
    public string Import(string path, ThemeImageKind kind) => _images.Import(path, kind);

    /// <summary>Where a theme's image is on disk, or null if there isn't one (or it's gone).</summary>
    public string? ImagePath(string? name) => _images.Resolve(name);

    /// <summary>Saves now if a save is pending (also called on exit), then tidies up images no theme uses.</summary>
    public void Flush()
    {
        if (!_saveTimer.IsEnabled) return;
        _saveTimer.Stop();
        try
        {
            _store.Save(Current);
        }
        catch (Exception ex)
        {
            _log.Write(ex, "Could not save themes.json.");
            return; // don't delete images the saved file might still point at
        }

        try
        {
            _images.RemoveUnused(Current.InUse);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Write(ex, "Could not tidy up the images folder.");
        }
    }
}
