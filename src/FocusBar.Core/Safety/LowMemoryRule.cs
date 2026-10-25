namespace FocusBar.Core.Safety;

/// <summary>
/// "Close FocusBar when the computer is low on memory": on by default at 90% of RAM in use. Closing gives the Windows
/// taskbar back, so the student's work keeps running smoothly.
/// </summary>
public sealed record LowMemorySettings(bool Enabled, int ThresholdPercent)
{
    public const int DefaultThreshold = 90;

    /// <summary>The levels offered in Settings.</summary>
    public static readonly IReadOnlyList<int> Choices = [80, 85, 90, 95];

    public static LowMemorySettings Default => new(true, DefaultThreshold);

    /// <summary>A missing section gets the defaults; a missing or odd threshold gets 90%.</summary>
    public static LowMemorySettings Clean(LowMemorySettings? settings) =>
        settings is null ? Default
        : settings with { ThresholdPercent = settings.ThresholdPercent is >= 50 and <= 99 ? settings.ThresholdPercent : DefaultThreshold };
}

/// <summary>
/// Decides when memory has been too full for long enough to close FocusBar. A short spike (opening a big app, a
/// game loading a level) shouldn't close it, so memory use has to stay above the threshold for <see cref="SustainFor"/>.
/// </summary>
public sealed class LowMemoryRule
{
    public static readonly TimeSpan SustainFor = TimeSpan.FromSeconds(30);

    private DateTimeOffset? _highSince;

    /// <summary>When the current stretch of high memory use started, or null if memory isn't high right now.</summary>
    public DateTimeOffset? HighSince => _highSince;

    /// <summary>Feeds one reading. Returns true once memory use has stayed above the threshold for 30 seconds.</summary>
    public bool Observe(double usedPercent, int thresholdPercent, DateTimeOffset now)
    {
        if (double.IsNaN(usedPercent) || usedPercent <= thresholdPercent)
        {
            _highSince = null;
            return false;
        }

        _highSince ??= now;
        return now - _highSince.Value >= SustainFor;
    }

    /// <summary>Starts over (for example when the setting is turned off).</summary>
    public void Reset() => _highSince = null;

    /// <summary>Percent of physical memory in use, from Windows' total and available bytes (NaN if unknown).</summary>
    public static double UsedPercent(ulong totalBytes, ulong availableBytes) =>
        totalBytes == 0 || availableBytes > totalBytes ? double.NaN : (1 - (double)availableBytes / totalBytes) * 100;
}
