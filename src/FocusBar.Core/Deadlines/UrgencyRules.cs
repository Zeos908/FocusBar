using FocusBar.Core.Models;

namespace FocusBar.Core.Deadlines;

/// <summary>
/// The single place where urgency thresholds live. Change them here and everything else follows.
/// </summary>
public static class UrgencyRules
{
    /// <summary>More time left than this is <see cref="Urgency.Low"/> (green).</summary>
    public static readonly TimeSpan MediumThreshold = TimeSpan.FromDays(3);

    /// <summary>Less time left than this is <see cref="Urgency.High"/> (red).</summary>
    public static readonly TimeSpan HighThreshold = TimeSpan.FromHours(24);

    public static Urgency Classify(DateTimeOffset dueAt, DateTimeOffset now)
    {
        var remaining = dueAt - now;

        if (remaining <= TimeSpan.Zero) return Urgency.Overdue;
        if (remaining < HighThreshold) return Urgency.High;
        if (remaining <= MediumThreshold) return Urgency.Medium;
        return Urgency.Low;
    }

    /// <summary>A completed deadline is never urgent, even if its due time has passed.</summary>
    public static Urgency Classify(Deadline deadline, DateTimeOffset now) =>
        deadline.IsComplete ? Urgency.Low : Classify(deadline.DueAt, now);
}
