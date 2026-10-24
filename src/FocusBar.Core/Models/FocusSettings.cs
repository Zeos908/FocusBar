namespace FocusBar.Core.Models;

public record FocusSettings(
    bool AutoEnable,
    TimeSpan StartBeforeDeadline,
    Strictness Strictness,
    TimeSpan SoftOverrideDelay,
    List<string> BlockedApps,      // process names, e.g. "Discord"
    List<string> BlockedSites)     // domains, e.g. "youtube.com"
{
    public static FocusSettings Default => new(
        AutoEnable: true,
        StartBeforeDeadline: TimeSpan.FromHours(2),
        Strictness: Strictness.Soft,
        SoftOverrideDelay: TimeSpan.FromSeconds(60),
        BlockedApps: [],
        BlockedSites: []);
}
