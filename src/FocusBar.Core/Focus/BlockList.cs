using FocusBar.Core.Models;

namespace FocusBar.Core.Focus;

/// <summary>Rules for what can go on the Focus Mode block lists.</summary>
public static class BlockList
{
    public const int MaxEntries = 200;

    private const string WindowsInvalidNameChars = "<>:\"/\\|?*";

    /// <summary>Common distracting sites, offered as one-click additions in Settings.</summary>
    public static IReadOnlyList<string> SuggestedSites { get; } =
        ["youtube.com", "tiktok.com", "instagram.com", "reddit.com", "twitch.tv", "netflix.com", "roblox.com", "discord.com", "x.com", "snapchat.com"];

    /// <summary>
    /// Windows and FocusBar programs that must never be closed: blocking them would break Windows, the bar itself,
    /// or apps that depend on them.
    /// </summary>
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "focusbar", "focusbar.blocker", "explorer", "dwm", "winlogon", "logonui", "lockapp", "userinit", "csrss", "smss",
        "wininit", "services", "lsass", "svchost", "system", "idle", "registry", "memory compression", "taskmgr",
        "conhost", "runtimebroker", "sihost", "ctfmon", "fontdrvhost", "searchhost", "searchapp", "searchui",
        "startmenuexperiencehost", "shellexperiencehost", "textinputhost", "applicationframehost", "msedgewebview2",
        "securityhealthsystray", "securityhealthservice", "msmpeng", "audiodg", "spoolsv", "dllhost", "taskhostw",
        "smartscreen", "consent", "werfault", "systemsettings",
    };

    /// <summary>"Discord.exe", " discord " → "Discord" (the name Windows lists the process under), or null.</summary>
    public static string? NormalizeAppName(string? input)
    {
        var name = input?.Trim().Trim('"');
        if (string.IsNullOrEmpty(name)) return null;
        // A full path ("C:\Games\game.exe") → just the file name. Windows rules, so tests behave the same everywhere.
        name = name[(name.LastIndexOfAny(['\\', '/']) + 1)..];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        name = name.Trim();
        if (name.Length is 0 or > 100 || name.Any(c => c < ' ' || WindowsInvalidNameChars.Contains(c))) return null;
        return name;
    }

    public static bool IsProtected(string appName) => Protected.Contains(NormalizeAppName(appName) ?? appName);

    /// <summary>Whether a running process (by its process name) is on the block list.</summary>
    public static bool IsBlocked(string processName, FocusSettings settings) =>
        !IsProtected(processName) &&
        settings.BlockedApps.Any(app => string.Equals(NormalizeAppName(app), processName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Cleans a saved settings object: valid, unique, allowed entries only.</summary>
    public static FocusSettings Clean(FocusSettings settings)
    {
        var apps = (settings.BlockedApps ?? [])
            .Select(NormalizeAppName).OfType<string>()
            .Where(a => !IsProtected(a))
            .DistinctBy(a => a.ToLowerInvariant())
            .Take(MaxEntries).ToList();
        var sites = (settings.BlockedSites ?? [])
            .Select(HostsFile.NormalizeDomain).OfType<string>()
            .Distinct()
            .Take(MaxEntries).ToList();

        return settings with
        {
            BlockedApps = apps,
            BlockedSites = sites,
            StartBeforeDeadline = settings.StartBeforeDeadline <= TimeSpan.Zero || settings.StartBeforeDeadline > TimeSpan.FromDays(2)
                ? FocusSettings.Default.StartBeforeDeadline
                : settings.StartBeforeDeadline,
            SoftOverrideDelay = settings.SoftOverrideDelay < TimeSpan.FromSeconds(5) || settings.SoftOverrideDelay > TimeSpan.FromMinutes(30)
                ? FocusSettings.Default.SoftOverrideDelay
                : settings.SoftOverrideDelay,
            Strictness = Enum.IsDefined(settings.Strictness) ? settings.Strictness : FocusSettings.Default.Strictness,
        };
    }

    /// <summary>
    /// Websites can't show a warning the way apps can, so in Gentle mode they aren't blocked at all;
    /// Soft and Strict block them for the whole session.
    /// </summary>
    public static bool BlocksWebsites(Strictness strictness) => strictness != Strictness.Gentle;
}
