namespace FocusBar.Core.Reminders;

/// <param name="Enabled">Whether to show reminders at all.</param>
/// <param name="Offsets">How long before each deadline to remind, for example 1 day and 1 hour.</param>
public record ReminderSettings(bool Enabled, List<TimeSpan> Offsets)
{
    /// <summary>1 day, 3 hours, and 1 hour before, from the build instructions.</summary>
    public static ReminderSettings Default => new(true, [TimeSpan.FromDays(1), TimeSpan.FromHours(3), TimeSpan.FromHours(1)]);

    /// <summary>The choices offered in Settings.</summary>
    public static IReadOnlyList<TimeSpan> Choices { get; } =
    [
        TimeSpan.FromDays(7),
        TimeSpan.FromDays(2),
        TimeSpan.FromDays(1),
        TimeSpan.FromHours(3),
        TimeSpan.FromHours(1),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(15),
    ];

    /// <summary>"1 week", "2 days", "3 hours", "30 minutes".</summary>
    public static string Describe(TimeSpan offset)
    {
        if (offset.TotalDays >= 7 && offset.TotalDays % 7 == 0) return Plural((int)(offset.TotalDays / 7), "week");
        if (offset.TotalDays >= 1 && offset.TotalHours % 24 == 0) return Plural((int)offset.TotalDays, "day");
        if (offset.TotalHours >= 1 && offset.TotalMinutes % 60 == 0) return Plural((int)offset.TotalHours, "hour");
        return Plural((int)offset.TotalMinutes, "minute");
    }

    private static string Plural(int count, string unit) => count == 1 ? $"1 {unit}" : $"{count} {unit}s";
}
