using FocusBar.Core.Deadlines;
using FocusBar.Core.Models;

namespace FocusBar.Core.Reminders;

/// <param name="Deadline">The deadline to remind about.</param>
/// <param name="Offset">Which reminder this is ("1 day before"), or null for a snoozed reminder coming back.</param>
/// <param name="IsCatchUp">True when FocusBar was off at the reminder's time and is showing it late.</param>
public record Reminder(Deadline Deadline, TimeSpan? Offset, bool IsCatchUp);

/// <summary>
/// Decides which reminders to show right now. Called every 30 seconds with the current deadlines.
/// Rules:
/// <list type="bullet">
/// <item>Each reminder (deadline + "how long before") shows once. Changing the due date sets up fresh reminders.</item>
/// <item>If several reminders for one deadline came due while FocusBar was off, only the latest one is shown.</item>
/// <item>Reminders that were already due before FocusBar first saw the deadline are skipped, so adding a
/// deadline due in 2 hours doesn't instantly pop up its "1 day before" reminder.</item>
/// <item>No reminders after a deadline is due or once it's complete. A snooze still comes back.</item>
/// </list>
/// </summary>
public static class ReminderPlanner
{
    /// <summary>Reminders are considered "late" (catch-up) after this long, e.g. the PC was asleep.</summary>
    private static readonly TimeSpan LateAfter = TimeSpan.FromMinutes(2);

    /// <summary>A deadline added a moment before one of its reminder times still gets that reminder.</summary>
    private static readonly TimeSpan FirstSeenGrace = TimeSpan.FromMinutes(1);

    /// <summary>Identifies one reminder. Includes the due time, so editing the due date re-arms reminders.</summary>
    public static string Key(Deadline deadline, TimeSpan offset) =>
        $"{deadline.Id:N}|{deadline.DueAt.UtcTicks}|{(long)offset.TotalMinutes}";

    /// <summary>
    /// Returns the reminders to show now and updates <paramref name="state"/> (the caller saves it).
    /// </summary>
    /// <param name="deadlines">All deadlines (completed ones are ignored).</param>
    public static IReadOnlyList<Reminder> Due(
        IEnumerable<Deadline> deadlines, ReminderSettings settings, ReminderState state, DateTimeOffset now)
    {
        var reminders = new List<Reminder>();

        foreach (var deadline in deadlines)
        {
            if (deadline.IsComplete)
            {
                state.Snoozed.Remove(deadline.Id);
                continue;
            }

            if (!state.FirstSeen.TryGetValue(deadline.Id, out var firstSeen))
            {
                firstSeen = now;
                state.FirstSeen[deadline.Id] = now;
            }

            var snoozeDue = state.Snoozed.TryGetValue(deadline.Id, out var until) && until <= now;
            if (snoozeDue) state.Snoozed.Remove(deadline.Id);

            // Reminder times that have arrived and haven't been handled yet.
            var arrived = new List<(TimeSpan Offset, DateTimeOffset At)>();
            foreach (var offset in settings.Offsets.Distinct())
            {
                var key = Key(deadline, offset);
                var at = deadline.DueAt - offset;
                if (state.Fired.Contains(key) || at > now) continue;

                if (at < firstSeen - FirstSeenGrace || deadline.DueAt <= now || !settings.Enabled)
                {
                    state.Fired.Add(key); // skipped for good, never shown
                    continue;
                }
                arrived.Add((offset, at));
            }

            foreach (var (offset, _) in arrived) state.Fired.Add(Key(deadline, offset));

            if (snoozeDue)
            {
                reminders.Add(new Reminder(deadline, null, false));
            }
            else if (arrived.Count > 0)
            {
                // Only the one closest to the due time, e.g. "due in 1 hour" rather than also "due in 1 day".
                var latest = arrived.MinBy(r => r.Offset);
                reminders.Add(new Reminder(deadline, latest.Offset, now - latest.At > LateAfter));
            }
        }

        return reminders;
    }

    /// <summary>"Bio lab is due in 3 hours", "Bio lab is due now", "Bio lab was due 10 minutes ago".</summary>
    public static string Headline(Deadline deadline, DateTimeOffset now)
    {
        var remaining = deadline.DueAt - now;
        if (remaining > TimeSpan.FromMinutes(1)) return $"{deadline.Title} is due {Countdown.Describe(deadline.DueAt, now)}";
        if (remaining > -TimeSpan.FromMinutes(1)) return $"{deadline.Title} is due now";
        return $"{deadline.Title} was due {Countdown.Describe(deadline.DueAt, now)}";
    }
}
