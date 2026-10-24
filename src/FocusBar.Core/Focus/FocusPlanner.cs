using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;

namespace FocusBar.Core.Focus;

public enum FocusReason
{
    /// <summary>Turned on automatically because a deadline is close.</summary>
    Deadline,

    /// <summary>The student started a timed session ("Focus for 25 min").</summary>
    Manual,
}

/// <param name="Reason">Why Focus Mode is on.</param>
/// <param name="EndsAt">When it turns off by itself.</param>
/// <param name="Deadline">For an automatic session, the deadline that caused it.</param>
public record FocusSession(FocusReason Reason, DateTimeOffset StartedAt, DateTimeOffset EndsAt, Deadline? Deadline)
{
    /// <summary>Identifies an automatic session, so "End focus" can turn off just that one.</summary>
    public string? DismissKey => Deadline is null ? null : FocusPlanner.DismissKey(Deadline);
}

/// <summary>A timed session the student started, saved so it survives a restart.</summary>
public record ManualSession(DateTimeOffset StartedAt, DateTimeOffset EndsAt);

/// <summary>
/// Decides whether Focus Mode should be on right now. Re-evaluated every few seconds and whenever deadlines
/// or settings change, so marking the deadline complete ends its session right away.
/// <list type="bullet">
/// <item>A manual session runs until its timer ends.</item>
/// <item>Otherwise, with automatic Focus Mode on and something on the block lists, it turns on
/// <see cref="FocusSettings.StartBeforeDeadline"/> before the soonest incomplete deadline and ends when that
/// deadline is due (or completed).</item>
/// <item>In Gentle and Soft, the student can end an automatic session early; it then stays off for that deadline.
/// In Strict, it can't be ended early (but completing the deadline still ends it).</item>
/// </list>
/// </summary>
public static class FocusPlanner
{
    public static FocusSession? Current(
        FocusSettings settings,
        IEnumerable<Deadline> deadlines,
        ManualSession? manual,
        IReadOnlySet<string> dismissed,
        DateTimeOffset now)
    {
        if (manual is not null && manual.StartedAt <= now && now < manual.EndsAt)
        {
            return new FocusSession(FocusReason.Manual, manual.StartedAt, manual.EndsAt, null);
        }

        if (!settings.AutoEnable || !HasAnythingToBlock(settings)) return null;

        var deadline = deadlines
            .Where(d => !d.IsComplete && d.DueAt > now && now >= d.DueAt - settings.StartBeforeDeadline)
            .Where(d => settings.Strictness == Strictness.Strict || !dismissed.Contains(DismissKey(d)))
            .OrderBy(d => d.DueAt)
            .FirstOrDefault();

        return deadline is null
            ? null
            : new FocusSession(FocusReason.Deadline, deadline.DueAt - settings.StartBeforeDeadline, deadline.DueAt, deadline);
    }

    /// <summary>Whether the student may end this session early.</summary>
    public static bool CanEndEarly(FocusSession session, FocusSettings settings) => settings.Strictness != Strictness.Strict;

    /// <summary>Includes the due time, so moving the deadline later turns automatic focus back on for it.</summary>
    public static string DismissKey(Deadline deadline) => $"{deadline.Id:N}|{deadline.DueAt.UtcTicks}";

    public static bool HasAnythingToBlock(FocusSettings settings) =>
        settings.BlockedApps.Count > 0 || (settings.BlockedSites.Count > 0 && BlockList.BlocksWebsites(settings.Strictness));

    /// <summary>"1h 42m left" style text for the bar.</summary>
    public static string Remaining(FocusSession session, DateTimeOffset now) => Countdown.Format(session.EndsAt, now);
}
