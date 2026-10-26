using FocusBar.Core.Focus;
using FocusBar.Core.Models;
using FocusBar.Core.Reminders;
using FocusBar.Core.Safety;
using FocusBar.Core.Storage;

namespace FocusBar.Core.Settings;

/// <summary>
/// The user's settings, saved to <c>settings.json</c>: reminders, Focus Mode, low-memory closing, and whether the
/// setup guide has run.
/// (The look of the bar is in <c>themes.json</c>.)
/// </summary>
public record AppSettings(ReminderSettings Reminders, FocusSettings Focus)
{
    public const string FileName = "settings.json";

    /// <summary>The first-run setup guide was finished or skipped. False in a new (or pre-Phase 8) settings file.</summary>
    public bool SetupCompleted { get; init; }

    /// <summary>
    /// The Windows taskbar's pinned apps were offered once (copied in if FocusBar had no pins of its own), so it
    /// doesn't happen again after the student unpins them.
    /// </summary>
    public bool WindowsPinsImported { get; init; }

    /// <summary>Close FocusBar (giving the Windows taskbar back) when the computer runs low on memory.</summary>
    public LowMemorySettings LowMemory { get; init; } = LowMemorySettings.Default;

    public static AppSettings Default => new(ReminderSettings.Default, FocusSettings.Default);
}

/// <summary>Loads and saves <see cref="AppSettings"/>, filling in anything missing with defaults.</summary>
public sealed class SettingsStore
{
    private readonly JsonFileStore _store;

    public SettingsStore(JsonFileStore store)
    {
        _store = store;
    }

    public AppSettings Load()
    {
        var loaded = _store.Load<AppSettings?>(AppSettings.FileName, null) ?? AppSettings.Default;

        // Sections missing from an older or hand-edited file get their defaults.
        var reminders = loaded.Reminders ?? ReminderSettings.Default;
        reminders = reminders with
        {
            Offsets = [.. (reminders.Offsets ?? []).Where(o => o > TimeSpan.Zero).Distinct().OrderDescending()],
        };
        return loaded with
        {
            Reminders = reminders,
            Focus = BlockList.Clean(loaded.Focus ?? FocusSettings.Default),
            LowMemory = LowMemorySettings.Clean(loaded.LowMemory),
        };
    }

    public void Save(AppSettings settings) => _store.Save(AppSettings.FileName, settings);
}
