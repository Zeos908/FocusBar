namespace FocusBar.Core.Media;

/// <summary>How far along a song or video is, ready to show.</summary>
public readonly record struct TimelineView(double Fraction, string Elapsed, string Total);

/// <summary>
/// Turns what Windows reports about the music or video that's playing into text for the bar: the app's name, the two
/// lines on the button, and the progress bar.
/// </summary>
public static class MediaText
{
    // Apps whose media IDs don't read well on their own.
    private static readonly Dictionary<string, string> KnownApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = "Chrome",
        ["msedge"] = "Microsoft Edge",
        ["MSEdge"] = "Microsoft Edge",
        ["firefox"] = "Firefox",
        ["308046B0AF4A39CB"] = "Firefox", // Firefox's media ID
        ["spotify"] = "Spotify",
        ["opera"] = "Opera",
        ["brave"] = "Brave",
        ["vlc"] = "VLC",
        ["ZuneMusic"] = "Media Player",
        ["ZuneVideo"] = "Movies & TV",
        ["MediaPlayer"] = "Media Player",
        ["AppleMusic"] = "Apple Music",
    };

    /// <summary>
    /// A readable app name from the ID Windows gives the media session, such as "Spotify.exe", "Chrome", or a Store app ID
    /// like "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify".
    /// </summary>
    public static string AppName(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return "an app";
        var id = appId.Trim();

        var bang = id.IndexOf('!');
        if (bang >= 0 && bang < id.Length - 1) id = id[(bang + 1)..];          // Store app: the app part of "package!app"
        id = id[(id.LastIndexOfAny(['\\', '/']) + 1)..];                       // a full path
        if (id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) id = id[..^4];

        if (KnownApps.TryGetValue(id, out var known)) return known;
        var lastPart = id[(id.LastIndexOf('.') + 1)..];                         // "Microsoft.ZuneMusic" → "ZuneMusic"
        if (KnownApps.TryGetValue(lastPart, out known)) return known;
        return string.IsNullOrWhiteSpace(lastPart) ? id : lastPart;
    }

    /// <summary>The two lines on the button: the title, and the artist (or album, or app).</summary>
    public static (string Title, string Subtitle) Lines(string? title, string? artist, string? album, string appName)
    {
        var subtitle = FirstOf(artist, album) ?? appName;
        return string.IsNullOrWhiteSpace(title) ? ($"Playing in {appName}", FirstOf(artist, album) ?? "") : (title.Trim(), subtitle);
    }

    /// <summary>
    /// Progress for the bar, or null when there's nothing to show (a live stream, or the app doesn't report it). Windows
    /// only updates the position now and then, so while playing it's moved forward by the time since that update.
    /// </summary>
    public static TimelineView? Timeline(TimeSpan start, TimeSpan end, TimeSpan position, DateTimeOffset lastUpdated,
        DateTimeOffset now, bool playing)
    {
        var length = end - start;
        if (length <= TimeSpan.Zero) return null;

        var elapsed = position - start;
        if (playing && lastUpdated > DateTimeOffset.MinValue && now > lastUpdated) elapsed += now - lastUpdated;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        if (elapsed > length) elapsed = length;

        return new TimelineView(elapsed / length, FormatTime(elapsed), FormatTime(length));
    }

    /// <summary>"3:07", or "1:02:45" for an hour or more.</summary>
    public static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{(int)time.TotalMinutes}:{time.Seconds:00}";

    private static string? FirstOf(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
